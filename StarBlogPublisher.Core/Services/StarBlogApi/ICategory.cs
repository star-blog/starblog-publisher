using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;
using StarBlogPublisher.Models;
using StarBlogPublisher.Models.Dtos;

namespace StarBlogPublisher.Services.StarBlogApi;

public interface ICategory {
    [Get("/api/v1/categories/tree")]
    Task<List<Category>?> GetNodes();
    
    [Get("/api/v1/categories/word-cloud")]
    Task<List<WordCloud>> GetWordCloud();

    [Get("/api/v1/categories")]
    Task<PageResult<Category>> GetList(int page = 1, int pageSize = 100);

    [Post("/api/v1/categories")]
    Task<Category> Add([Body] CategoryCreationDto dto);

    [Put("/api/v1/categories/{id}")]
    Task<Category> Update(int id, [Body] CategoryCreationDto dto);

    [Delete("/api/v1/categories/{id}")]
    Task Delete(int id);
}
