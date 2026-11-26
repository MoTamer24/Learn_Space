namespace LearnSpace.Api.Modules.Content;

public enum TutoringMaterialType { File, Video, Notes }
public class TutoringMaterial
{
    public Guid Id { get; set; }
    public Guid TeacherId { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? SessionId { get; set; }
    public string Title { get; set; } = "";
    public TutoringMaterialType Type { get; set; }
    public string Url { get; set; } = "";
    public decimal SizeMB { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
