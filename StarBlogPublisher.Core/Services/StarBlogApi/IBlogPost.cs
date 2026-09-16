using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;
using StarBlogPublisher.Models;
using StarBlogPublisher.Models.Dtos;

namespace StarBlogPublisher.Services.StarBlogApi;

public interface IBlogPost {
    /// <summary>
    /// 发表文章
    /// </summary>
    [Post("/Api/BlogPost")]
    Task<StarBlogPublisher.Models.ApiResponse<BlogPost>> Add(PostCreationDto dto);
    
    /// <summary>
    /// 获取文章详情
    /// </summary>
    [Get("/Api/BlogPost/{id}")]
    Task<StarBlogPublisher.Models.ApiResponse<BlogPost>> Get(string id);

    /// <summary>
    /// 更新文章
    /// </summary>
    [Put("/Api/BlogPost/{id}")]
    Task<StarBlogPublisher.Models.ApiResponse<BlogPost>> Update(string id, PostUpdateDto dto);

    /// <summary>
    /// 上传图片
    /// </summary>
    [Multipart]
    [Post("/Api/BlogPost/{id}/UploadImage")]
    Task<StarBlogPublisher.Models.ApiResponse<UploadImageResult>> UploadImage(string id, StreamPart file);

    /// <summary>
    /// 获取文章里的图片
    /// </summary>
    [Get("/Api/BlogPost/{id}/Images")]
    Task<StarBlogPublisher.Models.ApiResponse<List<string>>> GetImages(string id);
}
