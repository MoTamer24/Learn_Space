# LearnSpace Backend (MVP)

> **Scope note:** payments, attendance, exams/assessments, materials, notifications and subscriptions were removed from this branch. Only Groups and Students (plus Identity, Catalog and Enrollment) remain. Sections below that still describe the removed features (seed accounts, walkthroughs, project structure, decisions) are out of date.

Backend API demo for individual tutors and students, with the original course-learning MVP retained alongside group tutoring, attendance, billing, assessments, and subscriptions.

- .NET 10, ASP.NET Core Web API (controllers)
- EF Core + PostgreSQL (Npgsql), ASP.NET Core Identity, JWT bearer
- One project, one folder per module, no extra layers (no MediatR, CQRS, repositories, AutoMapper)

---

## 1. What you need

| Tool | Version |
|---|---|
| .NET SDK | 10.x |
| Docker Desktop (or Docker Engine + compose) | any recent |

## 2. Run it

```bash
# 1) start PostgreSQL
docker compose up -d

# 2) run the API (Development environment is set in launchSettings.json)
cd LearnSpace.Api
dotnet run
```

Open **http://localhost:5080/swagger**.

On startup in Development the app creates the schema, creates all five roles, and loads both the original course seed and the tutoring demo seed. Uploaded files go to `LearnSpace.Api/storage/`.

> **Before real use:** replace `Jwt:Key` in `LearnSpace.Api/appsettings.json` (a long random string, at least 32 characters). The value in the repo is a development placeholder.

## 3. Seed accounts

All seeded accounts use the password **`Dev@12345`**.

| Role | Email | Notes |
|---|---|---|
| Admin | `admin@learnspace.dev` | demo administrator |
| Teacher | `ahmed.teacher@learnspace.dev` | owns "Introduction to C# Programming" (Published) and "Advanced ASP.NET Core" (Draft) |
| Teacher | `sara.teacher@learnspace.dev` | owns "Calculus I" (Published) |
| Student | `student1@learnspace.dev` | enrolled in monthly and per-session groups; partial payment recorded |
| Student | `student2@learnspace.dev` | enrollment is over its group debt limit; check-in is blocked |
| Student | `student3@learnspace.dev` | fully exempt from group fees |
| Parent | `parent1@learnspace.dev` | linked to students 1 and 2; receives check-in notifications |
| Staff | `assistant@learnspace.dev` | belongs to Ahmed's teacher account |

The C# course also has: 2 materials (placeholder files), a published quiz (2 MCQ + 1 written, 20 minutes), one task with a deadline 7 days ahead and one task whose deadline already passed.

The tutoring demo adds Mathematics and Programming subject-levels, two study groups (monthly and per-session pricing), schedules and upcoming class sessions, a question bank and assessment, group materials, a subscription plan, and sample debt/payment balances.

## Tutoring demo flow

1. Log in at `POST /api/auth/login` as `ahmed.teacher@learnspace.dev` with `Dev@12345` and authorize Swagger with the returned access token.
2. Use `GET /api/groups` and `GET /api/identity/students` to find the seeded group, student profile, and QR code value. Use the group's `sessions` route to get a session ID.
3. Call `POST /api/groups/check-in` with `{ "studentId": "<student profile id>", "sessionId": "<session id>", "status": "Present" }`. Student 1 is allowed; Student 2 is blocked by the group's debt limit. The blocked check-in sends in-app notifications to the teacher and linked parent.
4. Record a partial payment with `POST /api/payments`. Monthly groups require `billingMonth` in `YYYY-MM`; per-session groups require `sessionId`. The enrollment and student balances are updated through module contracts.
5. Explore `/api/catalog`, `/api/assessments`, `/api/tutoring/materials`, `/api/notifications`, and `/api/subscriptions` in Swagger.

All demo accounts use `Dev@12345`. Public registration can create Student and Teacher accounts; an Admin token is needed to create Admin, Parent, or Staff accounts or manage catalog reference data.

The tutoring API routes are protected by JWT role checks. Each module queries its own entities; cross-module workflows use service interfaces. The shared `AppDbContext` follows the reference repository's modular-monolith convention.

## 4. Try it in Swagger (about 5 minutes)

1. `POST /api/auth/login` with `student1@learnspace.dev` / `Dev@12345`, copy `accessToken`, click **Authorize** (top right) and paste it.
2. `GET /api/courses`, pick the C# course id, then `POST /api/courses/{id}/enroll` (201 the first time, 200 afterwards).
3. `GET /api/courses/{id}/quizzes` to get the quiz id, then `POST /api/quizzes/{quizId}/attempts`. You receive `deadlineAt` and the questions (no `isCorrect`).
4. `PUT /api/attempts/{attemptId}/answers` with `[{ "questionId": "...", "selectedOptionId": "..." }, { "questionId": "<written>", "textAnswer": "..." }]`, then `POST /api/attempts/{attemptId}/submit`. The status is `PendingGrading` because the quiz has a written question.
5. Log in as `ahmed.teacher@learnspace.dev`, `GET /api/quizzes/{quizId}/attempts`, `GET /api/attempts/{attemptId}` (shows `answerId` values), then `POST /api/attempts/{attemptId}/grade` with `{ "answers": [{ "answerId": "<written answer id>", "awardedPoints": 5, "teacherNote": "Good" }] }`.
6. Back as the student, `GET /api/attempts/{attemptId}` now returns the score and per-question points.
7. Late task: as `student3`, `PUT /api/tasks/{closedTaskId}/submission` with a PDF. The first upload after the deadline is accepted with `isLate = true`; uploading again returns **409**.
8. Non-enrolled access: as `student2`, `GET /api/courses/{csharpId}/materials` returns **403**.
9. Teacher results table: `GET /api/courses/{id}/students`.

To test the **timed** behaviour quickly, create a quiz with `durationMinutes: 1` as the teacher, publish it, start it as a student, wait a minute and `PUT .../answers`: you get **409** and the attempt is finalized.

## 5. Endpoints

All routes are under `/api`. "T" = Teacher, "S" = Student.

| Area | Endpoint | Who |
|---|---|---|
| Auth | `POST /auth/register`, `POST /auth/login`, `GET /auth/me` | public / any signed in |
| Teachers | `GET /teachers`, `GET /teachers/{id}`, `GET /teachers/{id}/logo` | public |
| | `PUT /teachers/me/profile`, `POST /teachers/me/logo` (multipart), `GET /teachers/me/courses` | T |
| Courses | `GET /courses?subject=`, `GET /courses/{id}` | public (Draft: owner only) |
| | `POST /courses`, `PUT /courses/{id}`, `POST /courses/{id}/publish` | T (owner) |
| Enrollment | `POST /courses/{id}/enroll`, `GET /enrollments/me` | S |
| | `GET /courses/{id}/students` (student list) | T (owner) |
| Identity | `/identity/users`, `/identity/students`, `/identity/parents`, `/identity/student-parents`, `/identity/staff` | signed in (user/staff creation: Admin) |
| Catalog | `/catalog/subjects`, `/catalog/levels`, `/catalog/subject-levels` | reads public; writes Admin |
| Groups | `/groups`, `/groups/{id}/schedules`, `/groups/{id}/sessions`, `/groups/{id}/enrollments` | T/Admin/Staff |

Errors are `ProblemDetails`: 400 invalid input, 401 not signed in, 403 not allowed, 404 not found, 409 rule violation.

## 6. Configuration (`LearnSpace.Api/appsettings.json`)

| Key | Meaning |
|---|---|
| `ConnectionStrings:Default` | PostgreSQL connection (matches `docker-compose.yml`) |
| `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience` | token signing (lifetime is 60 minutes) |
| `Storage:RootPath` | where files are stored (relative paths are inside the API project folder) |
| `Storage:Uploads:*` | allowed extensions and max size (MB) per kind: Logo |
| `Cors:AllowedOrigin` | the frontend origin, default `http://localhost:3000` |

## 7. Database migrations

The spec asks for code-first migrations. The project was written without access to the .NET tooling, so **no migration files are included**. To keep "unzip and run" working, startup behaves like this in Development:

- no migrations in `Data/Migrations` → the schema is created straight from the model (`EnsureCreated`)
- migrations present → they are applied (`Migrate`)

To switch to real migrations (recommended before you change the model again):

```bash
docker compose down -v          # wipe the dev database created without migrations
dotnet tool install --global dotnet-ef
cd LearnSpace.Api
dotnet ef migrations add InitialCreate -o Data/Migrations
dotnet run                      # applies the migration, creates roles, seeds
```

For anything other than Development, run `dotnet ef database update` yourself and create the configured roles (automatic schema setup and demo seeding are Development-only).

## 8. Project structure

```
LearnSpace.sln
docker-compose.yml
LearnSpace.Api/
  Program.cs
  appsettings.json
  Common/
    Storage/        IStorageService, LocalDiskStorage, UploadValidator
    Errors/         AppException + exception-to-ProblemDetails middleware
    CurrentUser.cs  user id and role from the JWT
  Data/
    AppDbContext.cs
    Seed/DbSeeder.cs
    Migrations/     (empty until you run `dotnet ef migrations add`)
  Modules/
    Identity/       ApplicationUser, AuthController, TokenService, UserLookup
    Catalog/        TeacherProfile, Subject, Level, SubjectLevel, courses and catalog contracts
    Enrollment/     legacy course enrollment module
    Groups/         StudyGroup, GroupSchedule, GroupEnrollment, ClassSession
    Attendance/     check-in and attendance records
    Payments/       partial payments and enrollment balance refresh
    Content/        Material, MaterialsController, MaterialService
    Assessments/    course quizzes/tasks and tutoring question bank/assessments
    Notifications/  teacher/parent in-app notifications
    Subscriptions/  plans, teacher subscriptions and teacher payments
```

Module rule: each module queries its own entities. Cross-module calls go through small interfaces such as `IGroupBillingService`, `IPaymentService`, `IStudentDebtService`, `INotificationService`, `ICatalogLookup`, and `ITeacherService`. The API uses one shared `AppDbContext`, matching the reference repository's modular-monolith convention.

## 9. Decisions and assumptions (where the spec was silent)

- **Draft courses** requested by a non-owner (or anonymous) return 404, so their existence is not leaked. Enrolling in a Draft returns 409.
- **Enroll / start quiz / submit task** return 201 when something new is created and 200 when the existing one is returned.
- **Student quiz list** (`GET /courses/{id}/quizzes`) returns a summary (title, duration, question count, total points). The questions of a timed quiz are only delivered when the attempt starts.
- **Finalizing** creates an answer row for every question (unanswered ones too), so the teacher can grade unanswered written questions. A lazily finalized attempt gets `SubmittedAt = DeadlineAt`.
- **Submitting** an attempt that is already finalized is idempotent (returns the student's view instead of an error).
- **Teacher grading** requires points for every written answer; otherwise 400 (an attempt is never marked Graded with ungraded written answers). Only written answers can be graded manually.
- **Results table** (`/courses/{id}/students`): if a course has several quizzes or tasks, each student's most recent attempt / submission is shown. Status values: `NotStarted | InProgress | PendingGrading | Graded` and `NotSubmitted | Submitted | Graded`.
- **Quiz options** have no position column in the spec, so their order is stable but not guaranteed to match creation order.
- **Task deadlines** are accepted as `DateTimeOffset`; send them with `Z` or an offset (for example `2026-12-31T20:00:00Z`).
- **Quiz creation** may have zero questions (the spec only requires one at publish time), but such a quiz can never be published.
- Added one tiny helper beyond the spec: `GET /api/auth/me` to check a token from Swagger.
- File content is served with the content type derived from the file extension, never from what the client sent.
- Videos are protected by the JWT, so a plain `<video src>` cannot play them (browsers do not send the header). The frontend should fetch with the token and play a blob/object URL, or the backend can later add short-lived signed URLs.

## 10. Out of scope (intentionally not built)

Real payment-gateway integration, live video sessions, SignalR/real-time delivery, production authorization policies and tenant isolation, refresh tokens, S3, DRM, quiz retakes, multiple files per submission, AI features, unit tests, and a Docker image for the API.
