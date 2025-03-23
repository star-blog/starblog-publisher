using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Services.StarBlogApi;

public interface ICategory {
    [Get("/Api/Category/Nodes")]
    Task<CodeLab.Share.ViewModels.Response.ApiResponse<List<Category>>> GetNodes();
    
    [Get("/Api/Category/WordCloud")]
    Task<CodeLab.Share.ViewModels.Response.ApiResponse<List<WordCloud>>> GetWordCloud();
}