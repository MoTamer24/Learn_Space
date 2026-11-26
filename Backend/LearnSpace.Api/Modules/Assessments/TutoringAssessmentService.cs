using LearnSpace.Api.Data;
using LearnSpace.Api.Modules.Groups;
using Microsoft.EntityFrameworkCore;

namespace LearnSpace.Api.Modules.Assessments;

public class TutoringAssessmentService(AppDbContext db, IGroupBillingService groups)
{
    public Task<List<Assessment>> GetAssessmentsAsync(Guid? groupId, Guid? teacherId, IReadOnlyCollection<Guid>? groupIds, CancellationToken ct = default)
    { var query = db.TutoringAssessments.AsNoTracking(); if (groupId.HasValue) query = query.Where(x => x.GroupId == groupId); if (teacherId.HasValue) query = query.Where(x => x.TeacherId == teacherId); if (groupIds is not null) query = query.Where(x => groupIds.Contains(x.GroupId)); return query.OrderBy(x => x.Date).ToListAsync(ct); }
    public async Task<Assessment> CreateAsync(CreateTutoringAssessmentRequest r, CancellationToken ct = default)
    { if (string.IsNullOrWhiteSpace(r.Title) || r.Duration <= 0 || r.TotalMarks <= 0) throw new InvalidOperationException("Title, positive duration, and total marks are required."); if (!await groups.IsGroupOwnedByTeacherAsync(r.TeacherId, r.GroupId, ct)) throw new InvalidOperationException("Teacher does not own this group."); if (r.SessionId.HasValue && !await groups.IsSessionInGroupAsync(r.SessionId.Value, r.GroupId, ct)) throw new InvalidOperationException("Session does not belong to this group."); var x = new Assessment { TeacherId = r.TeacherId, GroupId = r.GroupId, SessionId = r.SessionId, Title = r.Title.Trim(), Type = r.Type, Date = r.Date, Duration = r.Duration, TotalMarks = r.TotalMarks }; db.TutoringAssessments.Add(x); await db.SaveChangesAsync(ct); return x; }
    public async Task<QuestionBankItem> AddQuestionAsync(CreateQuestionBankRequest r, CancellationToken ct = default)
    { if (string.IsNullOrWhiteSpace(r.QuestionText)) throw new InvalidOperationException("QuestionText is required."); var q = new QuestionBankItem { TeacherId = r.TeacherId, SubjectLevelId = r.SubjectLevelId, QuestionText = r.QuestionText.Trim(), Type = r.Type, ModelAnswer = r.ModelAnswer }; db.QuestionBankItems.Add(q); db.QuestionBankOptions.AddRange((r.Options ?? []).Select(o => new QuestionBankOption { QuestionId = q.Id, OptionText = o.OptionText, IsCorrect = o.IsCorrect })); await db.SaveChangesAsync(ct); return q; }
    public async Task<AssessmentQuestion> AddQuestionToAssessmentAsync(Guid assessmentId, Guid questionId, int order, decimal mark, CancellationToken ct = default)
    { if (order <= 0 || mark <= 0) throw new InvalidOperationException("Question order and mark must be positive."); if (!await db.TutoringAssessments.AnyAsync(x => x.Id == assessmentId, ct) || !await db.QuestionBankItems.AnyAsync(x => x.Id == questionId, ct)) throw new KeyNotFoundException("Assessment or question not found."); if (await db.AssessmentQuestions.AnyAsync(x => x.AssessmentId == assessmentId && (x.QuestionId == questionId || x.QuestionOrder == order), ct)) throw new InvalidOperationException("Question or order already exists on the assessment."); var link = new AssessmentQuestion { AssessmentId = assessmentId, QuestionId = questionId, QuestionOrder = order, Mark = mark }; db.AssessmentQuestions.Add(link); await db.SaveChangesAsync(ct); return link; }
    public Task<List<QuestionBankItem>> GetQuestionBankAsync(Guid? teacherId, CancellationToken ct = default)
    { var query = db.QuestionBankItems.AsNoTracking(); if (teacherId.HasValue) query = query.Where(x => x.TeacherId == teacherId); return query.OrderByDescending(x => x.CreatedAt).ToListAsync(ct); }
    public async Task<List<object>> GetAssessmentQuestionsAsync(Guid assessmentId, CancellationToken ct = default)
    {
        if (!await db.TutoringAssessments.AnyAsync(x => x.Id == assessmentId, ct)) throw new KeyNotFoundException("Assessment not found.");
        var links = await db.AssessmentQuestions.AsNoTracking().Where(x => x.AssessmentId == assessmentId).OrderBy(x => x.QuestionOrder).ToListAsync(ct);
        var result = new List<object>();
        foreach (var link in links)
        {
            var question = await db.QuestionBankItems.AsNoTracking().SingleAsync(x => x.Id == link.QuestionId, ct);
            var options = await db.QuestionBankOptions.AsNoTracking().Where(x => x.QuestionId == question.Id).Select(x => new { x.Id, x.OptionText }).ToListAsync(ct);
            result.Add(new { question.Id, link.QuestionOrder, link.Mark, question.QuestionText, question.Type, Options = options });
        }
        return result;
    }
    public async Task<StudentAssessment> SubmitAsync(Guid assessmentId, Guid studentId, IReadOnlyList<SubmitAnswerRequest> answers, CancellationToken ct = default)
    {
        var assessment = await db.TutoringAssessments.SingleOrDefaultAsync(x => x.Id == assessmentId, ct) ?? throw new KeyNotFoundException("Assessment not found.");
        if (!await groups.IsStudentActiveInGroupAsync(studentId, assessment.GroupId, ct)) throw new InvalidOperationException("Student is not actively enrolled in this assessment's group.");
        if (await db.StudentAssessments.AnyAsync(x => x.AssessmentId == assessmentId && x.StudentId == studentId, ct)) throw new InvalidOperationException("A submission already exists for this student.");
        if (answers.Select(x => x.QuestionId).Distinct().Count() != answers.Count) throw new InvalidOperationException("Each question can only be answered once.");
        var links = await db.AssessmentQuestions.AsNoTracking().Where(x => x.AssessmentId == assessmentId).ToDictionaryAsync(x => x.QuestionId, ct);
        var submission = new StudentAssessment { AssessmentId = assessmentId, StudentId = studentId, SubmittedAt = DateTime.UtcNow };
        db.StudentAssessments.Add(submission);
        var answeredIds = new HashSet<Guid>();
        foreach (var answer in answers)
        {
            if (!links.TryGetValue(answer.QuestionId, out var link)) throw new InvalidOperationException("Answer contains a question outside this assessment.");
            answeredIds.Add(answer.QuestionId);
            var questionType = await db.QuestionBankItems.Where(x => x.Id == answer.QuestionId).Select(x => x.Type).SingleAsync(ct);
            bool? correct = null; decimal? awarded = null;
            if (answer.SelectedOptionId is Guid optionId)
            {
                if (questionType != TutoringQuestionType.MCQ) throw new InvalidOperationException("Essay questions require AnswerText.");
                var option = await db.QuestionBankOptions.SingleOrDefaultAsync(x => x.Id == optionId && x.QuestionId == answer.QuestionId, ct) ?? throw new InvalidOperationException("Selected option does not belong to the question.");
                correct = option.IsCorrect; awarded = correct.Value ? link.Mark : 0;
            }
            else if (questionType == TutoringQuestionType.MCQ) { correct = false; awarded = 0; }
            db.StudentAnswers.Add(new StudentAnswer { StudentAssessmentId = submission.Id, QuestionId = answer.QuestionId, SelectedOptionId = answer.SelectedOptionId, AnswerText = answer.AnswerText, IsCorrect = correct, MarkAwarded = awarded });
        }
        foreach (var questionId in links.Keys.Where(id => !answeredIds.Contains(id)))
        {
            var questionType = await db.QuestionBankItems.Where(x => x.Id == questionId).Select(x => x.Type).SingleAsync(ct);
            db.StudentAnswers.Add(new StudentAnswer { StudentAssessmentId = submission.Id, QuestionId = questionId,
                IsCorrect = questionType == TutoringQuestionType.MCQ ? false : null,
                MarkAwarded = questionType == TutoringQuestionType.MCQ ? 0 : null });
        }
        await db.SaveChangesAsync(ct);
        var answered = await db.StudentAnswers.Where(x => x.StudentAssessmentId == submission.Id).ToListAsync(ct);
        var essays = await (from a in db.StudentAnswers join q in db.QuestionBankItems on a.QuestionId equals q.Id where a.StudentAssessmentId == submission.Id && q.Type == TutoringQuestionType.Essay select a.Id).AnyAsync(ct);
        if (!essays) submission.Mark = answered.Sum(x => x.MarkAwarded ?? 0);
        submission.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); return submission;
    }

    public async Task<StudentAssessment> GradeAsync(Guid submissionId, Guid teacherId, IReadOnlyList<GradeTutoringAnswerRequest> grades, CancellationToken ct = default)
    {
        var submission = await db.StudentAssessments.SingleOrDefaultAsync(x => x.Id == submissionId, ct) ?? throw new KeyNotFoundException("Submission not found.");
        var assessment = await db.TutoringAssessments.SingleAsync(x => x.Id == submission.AssessmentId, ct);
        if (!await groups.IsGroupOwnedByTeacherAsync(teacherId, assessment.GroupId, ct)) throw new InvalidOperationException("Teacher does not own this assessment's group.");
        foreach (var grade in grades)
        {
            var answer = await db.StudentAnswers.SingleOrDefaultAsync(x => x.Id == grade.AnswerId && x.StudentAssessmentId == submissionId, ct) ?? throw new KeyNotFoundException("Answer not found.");
            var questionMark = await db.AssessmentQuestions.Where(x => x.AssessmentId == assessment.Id && x.QuestionId == answer.QuestionId).Select(x => (decimal?)x.Mark).SingleOrDefaultAsync(ct) ?? 0;
            var questionType = await db.QuestionBankItems.Where(x => x.Id == answer.QuestionId).Select(x => x.Type).SingleAsync(ct);
            if (questionType != TutoringQuestionType.Essay || grade.MarkAwarded < 0 || grade.MarkAwarded > questionMark) throw new InvalidOperationException("Only essay answers may be graded, and marks must be within the question limit.");
            answer.MarkAwarded = grade.MarkAwarded; answer.UpdatedAt = DateTime.UtcNow;
        }
        var allAnswers = await db.StudentAnswers.Where(x => x.StudentAssessmentId == submissionId).ToListAsync(ct);
        var essayIds = await (from answer in db.StudentAnswers join question in db.QuestionBankItems on answer.QuestionId equals question.Id where answer.StudentAssessmentId == submissionId && question.Type == TutoringQuestionType.Essay select answer.Id).ToListAsync(ct);
        if (essayIds.All(id => allAnswers.Any(a => a.Id == id && a.MarkAwarded is not null))) submission.Mark = allAnswers.Sum(x => x.MarkAwarded ?? 0);
        submission.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); return submission;
    }
}

public record CreateQuestionBankRequest(Guid TeacherId, Guid SubjectLevelId, string QuestionText, TutoringQuestionType Type, string? ModelAnswer, List<QuestionBankOptionRequest>? Options);
public record QuestionBankOptionRequest(string OptionText, bool IsCorrect);
public record CreateTutoringAssessmentRequest(Guid TeacherId, Guid GroupId, Guid? SessionId, string Title, AssessmentType Type, DateTime Date, int Duration, decimal TotalMarks);
public record AddAssessmentQuestionRequest(Guid QuestionId, int QuestionOrder, decimal Mark);
public record SubmitAnswerRequest(Guid QuestionId, Guid? SelectedOptionId, string? AnswerText);
public record SubmitTutoringAssessmentRequest(Guid StudentId, List<SubmitAnswerRequest> Answers);
public record GradeTutoringAnswerRequest(Guid AnswerId, decimal MarkAwarded);
public record GradeTutoringAssessmentRequest(Guid TeacherId, List<GradeTutoringAnswerRequest> Answers);
