using LearnSpace.Api.Common.Errors;
using LearnSpace.Api.Data;
using LearnSpace.Api.Modules.Catalog;
using LearnSpace.Api.Modules.Enrollments;
using LearnSpace.Api.Modules.Identity;
using Microsoft.EntityFrameworkCore;

namespace LearnSpace.Api.Modules.Assessments;

public class QuizService(
    AppDbContext db,
    ICourseService courses,
    IEnrollmentService enrollments,
    IUserLookup users)
{
    // =====================================================================
    // Quiz definition (teacher)
    // =====================================================================

    public async Task<QuizForTeacherDto> CreateAsync(Guid teacherId, Guid courseId, CreateQuizRequest request)
    {
        await courses.RequireOwnerAsync(teacherId, courseId);

        var questionRequests = request.Questions ?? new List<CreateQuestionRequest>();
        foreach (var q in questionRequests) ValidateQuestion(q);

        var quiz = new Quiz
        {
            CourseId = courseId,
            Title = request.Title.Trim(),
            DurationMinutes = request.DurationMinutes,
            Status = QuizStatus.Draft
        };

        var position = 1;
        foreach (var q in questionRequests)
        {
            var question = new Question
            {
                Type = q.Type,
                Text = q.Text.Trim(),
                Points = q.Points,
                Position = position++
            };

            if (q.Type == QuestionType.Mcq)
            {
                foreach (var o in q.Options!)
                    question.Options.Add(new QuestionOption { Text = o.Text.Trim(), IsCorrect = o.IsCorrect });
            }

            quiz.Questions.Add(question);
        }

        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync();
        return ToTeacherDto(quiz);
    }

    public async Task<QuizForTeacherDto> PublishAsync(Guid teacherId, Guid quizId)
    {
        var quiz = await db.Quizzes
            .Include(x => x.Questions).ThenInclude(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == quizId)
            ?? throw AppException.NotFound("Quiz not found.");

        await courses.RequireOwnerAsync(teacherId, quiz.CourseId);

        if (quiz.Status == QuizStatus.Published) return ToTeacherDto(quiz);

        if (quiz.Questions.Count == 0)
            throw AppException.Conflict("A quiz needs at least one question before it can be published.");

        foreach (var mcq in quiz.Questions.Where(x => x.Type == QuestionType.Mcq))
        {
            if (mcq.Options.Count(o => o.IsCorrect) != 1)
                throw AppException.Conflict($"Question \"{mcq.Text}\" must have exactly one correct option.");
        }

        quiz.Status = QuizStatus.Published;
        await db.SaveChangesAsync();
        return ToTeacherDto(quiz);
    }

    public async Task<List<QuizForTeacherDto>> ListForTeacherAsync(Guid teacherId, Guid courseId)
    {
        await courses.RequireOwnerAsync(teacherId, courseId);

        var quizzes = await db.Quizzes.AsNoTracking()
            .Include(x => x.Questions).ThenInclude(x => x.Options)
            .Where(x => x.CourseId == courseId)
            .OrderBy(x => x.Title)
            .ToListAsync();

        return quizzes.Select(ToTeacherDto).ToList();
    }

    public async Task<List<QuizSummaryForStudentDto>> ListForStudentAsync(Guid studentId, Guid courseId)
    {
        await enrollments.RequireCanViewCourseAsync(studentId, courseId);

        var quizzes = await db.Quizzes.AsNoTracking()
            .Include(x => x.Questions)
            .Where(x => x.CourseId == courseId && x.Status == QuizStatus.Published)
            .OrderBy(x => x.Title)
            .ToListAsync();

        return quizzes
            .Select(x => new QuizSummaryForStudentDto(
                x.Id, x.CourseId, x.Title, x.DurationMinutes, x.Questions.Count, x.Questions.Sum(q => q.Points)))
            .ToList();
    }

    // =====================================================================
    // Taking a quiz (student)
    // =====================================================================

    /// <summary>Starts the attempt, or resumes it while it is still InProgress and before the deadline.</summary>
    public async Task<(AttemptStartDto Attempt, bool Created)> StartAsync(Guid studentId, Guid quizId)
    {
        var quiz = await LoadQuizAsync(quizId);

        // 403 comes before the "not published" check so outsiders learn nothing about the quiz.
        await enrollments.RequireCanViewCourseAsync(studentId, quiz.CourseId);

        if (quiz.Status != QuizStatus.Published)
            throw AppException.Conflict("This quiz is not published yet.");

        var attempt = await db.QuizAttempts.Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.QuizId == quizId && a.StudentId == studentId);

        var now = DateTime.UtcNow;

        if (attempt is not null)
        {
            if (attempt.Status == AttemptStatus.InProgress && now <= attempt.DeadlineAt)
                return (ToStartDto(attempt, quiz), false);

            // Past the deadline but never finalized: finalize now (rule 7), then refuse.
            await EnsureCurrentAsync(attempt, quiz);
            throw AppException.Conflict("You have already taken this quiz. Quizzes cannot be retaken.");
        }

        attempt = new QuizAttempt
        {
            QuizId = quizId,
            StudentId = studentId,
            StartedAt = now,
            DeadlineAt = now.AddMinutes(quiz.DurationMinutes),
            Status = AttemptStatus.InProgress
        };
        db.QuizAttempts.Add(attempt);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Double click: the unique (QuizId, StudentId) index rejected the second insert.
            db.Entry(attempt).State = EntityState.Detached;
            var existing = await db.QuizAttempts.Include(a => a.Answers)
                .FirstOrDefaultAsync(a => a.QuizId == quizId && a.StudentId == studentId);
            if (existing is null) throw;
            if (existing.Status == AttemptStatus.InProgress && DateTime.UtcNow <= existing.DeadlineAt)
                return (ToStartDto(existing, quiz), false);
            throw AppException.Conflict("You have already taken this quiz. Quizzes cannot be retaken.");
        }

        return (ToStartDto(attempt, quiz), true);
    }

    public async Task<StudentAttemptStatusDto> SaveAnswersAsync(Guid studentId, Guid attemptId, List<SaveAnswerRequest> answers)
    {
        var (attempt, quiz) = await LoadOwnAttemptAsync(studentId, attemptId);

        // Rule 6: the server owns the clock.
        if (attempt.Status == AttemptStatus.InProgress && DateTime.UtcNow > attempt.DeadlineAt)
        {
            await FinalizeAsync(attempt, quiz, attempt.DeadlineAt);
            throw AppException.Conflict("The deadline has passed. Your attempt was submitted automatically.");
        }

        if (attempt.Status != AttemptStatus.InProgress)
            throw AppException.Conflict("This attempt has already been submitted.");

        var questions = quiz.Questions.ToDictionary(x => x.Id);

        foreach (var request in answers)
        {
            if (!questions.TryGetValue(request.QuestionId, out var question))
                throw AppException.BadRequest($"Question {request.QuestionId} is not part of this quiz.");

            var row = attempt.Answers.FirstOrDefault(a => a.QuestionId == question.Id);
            if (row is null)
            {
                row = new AttemptAnswer { AttemptId = attempt.Id, QuestionId = question.Id };
                db.AttemptAnswers.Add(row);
                attempt.Answers.Add(row);
            }

            if (question.Type == QuestionType.Mcq)
            {
                if (request.SelectedOptionId is Guid optionId && question.Options.All(o => o.Id != optionId))
                    throw AppException.BadRequest($"Option {optionId} does not belong to question {question.Id}.");

                row.SelectedOptionId = request.SelectedOptionId;
                row.TextAnswer = null;
            }
            else
            {
                row.TextAnswer = request.TextAnswer;
                row.SelectedOptionId = null;
            }
        }

        await db.SaveChangesAsync();
        return ToStatusDto(attempt);
    }

    /// <summary>Idempotent: submitting an attempt that is already finalized just returns what the student may see.</summary>
    public async Task<object> SubmitAsync(Guid studentId, Guid attemptId)
    {
        var (attempt, quiz) = await LoadOwnAttemptAsync(studentId, attemptId);

        if (attempt.Status == AttemptStatus.InProgress)
        {
            var now = DateTime.UtcNow;
            await FinalizeAsync(attempt, quiz, now > attempt.DeadlineAt ? attempt.DeadlineAt : now);
        }

        return StudentView(attempt, quiz);
    }

    public async Task<object> GetForStudentAsync(Guid studentId, Guid attemptId)
    {
        var (attempt, quiz) = await LoadOwnAttemptAsync(studentId, attemptId);
        await EnsureCurrentAsync(attempt, quiz);
        return StudentView(attempt, quiz);
    }

    // =====================================================================
    // Teacher side of attempts
    // =====================================================================

    public async Task<TeacherAttemptDto> GetForTeacherAsync(Guid teacherId, Guid attemptId)
    {
        var (attempt, quiz) = await LoadAttemptAsync(attemptId);
        await courses.RequireOwnerAsync(teacherId, quiz.CourseId);
        await EnsureCurrentAsync(attempt, quiz);
        return await TeacherViewAsync(attempt, quiz);
    }

    public async Task<List<AttemptSummaryDto>> ListAttemptsAsync(Guid teacherId, Guid quizId)
    {
        var quiz = await LoadQuizAsync(quizId);
        await courses.RequireOwnerAsync(teacherId, quiz.CourseId);

        var attempts = await db.QuizAttempts.Include(a => a.Answers)
            .Where(a => a.QuizId == quizId)
            .ToListAsync();

        foreach (var attempt in attempts) await EnsureCurrentAsync(attempt, quiz);

        var names = await users.GetDisplayNamesAsync(attempts.Select(a => a.StudentId));

        return attempts
            .OrderBy(a => a.StartedAt)
            .Select(a => new AttemptSummaryDto(
                a.Id, a.StudentId, names.GetValueOrDefault(a.StudentId, ""), a.Status, a.Score, a.StartedAt, a.SubmittedAt))
            .ToList();
    }

    public async Task<TeacherAttemptDto> GradeAsync(Guid teacherId, Guid attemptId, GradeAttemptRequest request)
    {
        var (attempt, quiz) = await LoadAttemptAsync(attemptId);
        await courses.RequireOwnerAsync(teacherId, quiz.CourseId);
        await EnsureCurrentAsync(attempt, quiz);

        if (attempt.Status == AttemptStatus.InProgress)
            throw AppException.Conflict("The student is still taking this quiz.");

        var writtenQuestions = quiz.Questions.Where(x => x.Type == QuestionType.Written).ToDictionary(x => x.Id);

        foreach (var grade in request.Answers)
        {
            var answer = attempt.Answers.FirstOrDefault(a => a.Id == grade.AnswerId)
                ?? throw AppException.BadRequest($"Answer {grade.AnswerId} does not belong to this attempt.");

            if (!writtenQuestions.TryGetValue(answer.QuestionId, out var question))
                throw AppException.BadRequest("Only written answers are graded by the teacher; multiple choice is automatic.");

            if (grade.AwardedPoints < 0 || grade.AwardedPoints > question.Points)
                throw AppException.BadRequest($"Awarded points must be between 0 and {question.Points}.");

            answer.AwardedPoints = grade.AwardedPoints;
            answer.TeacherNote = string.IsNullOrWhiteSpace(grade.TeacherNote) ? null : grade.TeacherNote.Trim();
        }

        // Never mark an attempt Graded while a written answer still has no points.
        if (attempt.Answers.Any(a => writtenQuestions.ContainsKey(a.QuestionId) && a.AwardedPoints is null))
            throw AppException.BadRequest("Every written answer needs points before the attempt can be graded.");

        attempt.Score = attempt.Answers.Sum(a => a.AwardedPoints ?? 0);
        attempt.Status = AttemptStatus.Graded;
        await db.SaveChangesAsync();

        return await TeacherViewAsync(attempt, quiz);
    }

    /// <summary>Used by the results table: finalize one attempt if its time is up.</summary>
    public async Task EnsureAttemptCurrentAsync(Guid attemptId)
    {
        var (attempt, quiz) = await LoadAttemptAsync(attemptId);
        await EnsureCurrentAsync(attempt, quiz);
    }

    // =====================================================================
    // Finalizing (rules 7 and 8)
    // =====================================================================

    private async Task EnsureCurrentAsync(QuizAttempt attempt, Quiz quiz)
    {
        if (attempt.Status == AttemptStatus.InProgress && DateTime.UtcNow > attempt.DeadlineAt)
            await FinalizeAsync(attempt, quiz, attempt.DeadlineAt);
    }

    private async Task FinalizeAsync(QuizAttempt attempt, Quiz quiz, DateTime submittedAt)
    {
        foreach (var question in quiz.Questions)
        {
            // Make sure every question has an answer row, so the teacher can grade unanswered written questions too.
            var answer = attempt.Answers.FirstOrDefault(a => a.QuestionId == question.Id);
            if (answer is null)
            {
                answer = new AttemptAnswer { AttemptId = attempt.Id, QuestionId = question.Id };
                db.AttemptAnswers.Add(answer);
                attempt.Answers.Add(answer);
            }

            if (question.Type == QuestionType.Mcq)
            {
                var correct = question.Options.FirstOrDefault(o => o.IsCorrect);
                var isRight = correct is not null && answer.SelectedOptionId == correct.Id;
                answer.AwardedPoints = isRight ? question.Points : 0;
            }
        }

        attempt.SubmittedAt = submittedAt;

        if (quiz.Questions.Any(q => q.Type == QuestionType.Written))
        {
            attempt.Status = AttemptStatus.PendingGrading;   // Score stays null until the teacher grades
        }
        else
        {
            attempt.Score = attempt.Answers.Sum(a => a.AwardedPoints ?? 0);
            attempt.Status = AttemptStatus.Graded;
        }

        await db.SaveChangesAsync();
    }

    // =====================================================================
    // Loading helpers
    // =====================================================================

    private async Task<Quiz> LoadQuizAsync(Guid quizId)
        => await db.Quizzes.AsNoTracking()
            .Include(x => x.Questions).ThenInclude(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == quizId)
            ?? throw AppException.NotFound("Quiz not found.");

    private async Task<(QuizAttempt Attempt, Quiz Quiz)> LoadAttemptAsync(Guid attemptId)
    {
        var attempt = await db.QuizAttempts.Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == attemptId)
            ?? throw AppException.NotFound("Attempt not found.");

        var quiz = await LoadQuizAsync(attempt.QuizId);
        return (attempt, quiz);
    }

    private async Task<(QuizAttempt Attempt, Quiz Quiz)> LoadOwnAttemptAsync(Guid studentId, Guid attemptId)
    {
        var (attempt, quiz) = await LoadAttemptAsync(attemptId);
        if (attempt.StudentId != studentId)
            throw AppException.Forbidden("This is not your attempt.");
        return (attempt, quiz);
    }

    // =====================================================================
    // Validation and mapping
    // =====================================================================

    private static void ValidateQuestion(CreateQuestionRequest q)
    {
        var options = q.Options ?? new List<CreateOptionRequest>();

        if (q.Type == QuestionType.Mcq)
        {
            if (options.Count is < 2 or > 6)
                throw AppException.BadRequest("A multiple choice question needs 2 to 6 options.");
            if (options.Count(o => o.IsCorrect) != 1)
                throw AppException.BadRequest("A multiple choice question needs exactly one correct option.");
        }
        else if (options.Count > 0)
        {
            throw AppException.BadRequest("A written question cannot have options.");
        }
    }

    private static QuizForTeacherDto ToTeacherDto(Quiz quiz) => new(
        quiz.Id, quiz.CourseId, quiz.Title, quiz.DurationMinutes, quiz.Status,
        quiz.Questions
            .OrderBy(q => q.Position)
            .Select(q => new QuestionForTeacherDto(
                q.Id, q.Type, q.Text, q.Points, q.Position,
                q.Options.Select(o => new OptionForTeacherDto(o.Id, o.Text, o.IsCorrect)).ToList()))
            .ToList());

    private static AttemptStartDto ToStartDto(QuizAttempt attempt, Quiz quiz) => new(
        attempt.Id, quiz.Id, quiz.Title, attempt.Status, attempt.StartedAt, attempt.DeadlineAt,
        quiz.Questions
            .OrderBy(q => q.Position)
            .Select(q => new AttemptQuestionDto(
                q.Id, q.Type, q.Text, q.Points, q.Position,
                q.Options.Select(o => new OptionForStudentDto(o.Id, o.Text)).ToList()))
            .ToList(),
        attempt.Answers
            .Select(a => new SavedAnswerDto(a.QuestionId, a.SelectedOptionId, a.TextAnswer))
            .ToList());

    private static StudentAttemptStatusDto ToStatusDto(QuizAttempt a)
        => new(a.Id, a.QuizId, a.Status, a.StartedAt, a.DeadlineAt, a.SubmittedAt);

    /// <summary>Rule 10: only the status until Graded; after that the score and per-question points and notes.</summary>
    private static object StudentView(QuizAttempt attempt, Quiz quiz)
    {
        if (attempt.Status != AttemptStatus.Graded) return ToStatusDto(attempt);

        var answers = quiz.Questions
            .OrderBy(q => q.Position)
            .Select(q =>
            {
                var answer = attempt.Answers.FirstOrDefault(a => a.QuestionId == q.Id);
                return new StudentAnswerResultDto(q.Id, q.Text, q.Points, answer?.AwardedPoints, answer?.TeacherNote);
            })
            .ToList();

        return new StudentAttemptResultDto(attempt.Id, attempt.QuizId, attempt.Status, attempt.SubmittedAt, attempt.Score, answers);
    }

    private async Task<TeacherAttemptDto> TeacherViewAsync(QuizAttempt attempt, Quiz quiz)
    {
        var names = await users.GetDisplayNamesAsync([attempt.StudentId]);

        var answers = quiz.Questions
            .OrderBy(q => q.Position)
            .Select(q =>
            {
                var answer = attempt.Answers.FirstOrDefault(a => a.QuestionId == q.Id);
                QuestionOption? selected = null;
                if (answer?.SelectedOptionId is Guid selectedId)
                    selected = q.Options.FirstOrDefault(o => o.Id == selectedId);

                bool? isCorrect = q.Type == QuestionType.Mcq ? (selected?.IsCorrect ?? false) : null;

                return new TeacherAnswerDto(
                    answer?.Id, q.Id, q.Type, q.Text, q.Points,
                    answer?.SelectedOptionId, selected?.Text, isCorrect,
                    answer?.TextAnswer, answer?.AwardedPoints, answer?.TeacherNote);
            })
            .ToList();

        return new TeacherAttemptDto(
            attempt.Id, attempt.QuizId, attempt.StudentId, names.GetValueOrDefault(attempt.StudentId, ""),
            attempt.Status, attempt.StartedAt, attempt.DeadlineAt, attempt.SubmittedAt, attempt.Score, answers);
    }
}
