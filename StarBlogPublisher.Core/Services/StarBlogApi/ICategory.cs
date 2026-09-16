using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;
using StarBlogPublisher.Models;
using StarBlogPublisher.Models.Dtos;

namespace StarBlogPublisher.Services.StarBlogApi;

public interface ICategory {
    [Get("/Api/Category/Nodes")]
    Task<StarBlogPublisher.Models.ApiResponse<List<Category>>> GetNodes();
    
    [Get("/Api/Category/WordCloud")]
    Task<StarBlogPublisher.Models.ApiResponse<List<WordCloud>>> GetWordCloud();

    [Post("/Api/Category")]
    Task<StarBlogPublisher.Models.ApiResponse<Category>> Add(CategoryCreationDto dto);
}
