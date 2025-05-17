using System;
using System.Text.Json.Serialization;

namespace StarBlogPublisher.Models
{
    public class AIProfile
    {
        public string Name { get; set; } = string.Empty;
        public bool EnableAI { get; set; } = true;
        public string Provider { get; set; } = "openai";
        public string Key { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string ApiBase { get; set; } = string.Empty;

        [JsonIgnore]
        public string DisplayName => !string.IsNullOrEmpty(Name) ? Name : "默认配置";

        public AIProfile Clone()
        {
            return new AIProfile
            {
                Name = this.Name,
                EnableAI = this.EnableAI,
                Provider = this.Provider,
                Key = this.Key,
                Model = this.Model,
                ApiBase = this.ApiBase
            };
        }
    }
} 