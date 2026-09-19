namespace StarBlogPublisher.Utils;

public class PromptTemplate {
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;

    public override string ToString() {
        return Name;
    }
}
