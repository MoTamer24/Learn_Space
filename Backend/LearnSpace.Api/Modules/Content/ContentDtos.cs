namespace LearnSpace.Api.Modules.Content;

public record MaterialDto(
    Guid Id, Guid CourseId, string Title, MaterialType Type, string FileName, string ContentType, int Position);
