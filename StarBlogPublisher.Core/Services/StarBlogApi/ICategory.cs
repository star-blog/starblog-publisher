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

    [Get("/Api/Category/All")]
    Task<StarBlogPublisher.Models.ApiResponse<List<Category>>> GetAll();

    [Get("/Api/Category/{id}")]
    Task<StarBlogPublisher.Models.ApiResponse<Category>> Get(int id);

    [Post("/Api/Category")]
    Task<StarBlogPublisher.Models.ApiResponse<Category>> Add(CategoryCreationDto dto);

    [Put("/Api/Category/{id}")]
    Task<StarBlogPublisher.Models.ApiResponse<Category>> Update(int id, CategoryCreationDto dto);

    [Delete("/Api/Category/{id}")]
    Task<StarBlogPublisher.Models.ApiResponse<object>> Delete(int id);

    [Post("/Api/Category/{id}/SetVisible")]
    Task<StarBlogPublisher.Models.ApiResponse<object>> SetVisible(int id);

    [Post("/Api/Category/{id}/SetInvisible")]
    Task<StarBlogPublisher.Models.ApiResponse<object>> SetInvisible(int id);
}
