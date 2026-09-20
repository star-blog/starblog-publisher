using System;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace StarBlogPublisher.Models
{
    public class AIProfile : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        public string Name {
            get => _name;
            set {
                if (_name == value) return;
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
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
