using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;
using StarBlogPublisher.Models;
using StarBlogPublisher.Models.Dtos;

namespace StarBlogPublisher.Services.StarBlogApi;

public interface IBlogPost {
    /// <summary>
    /// 文章分页列表
    /// </summary>
    [Get("/api/v1/posts")]
    Task<PageResult<BlogPost>> GetList([Query] PostListQuery query);

    /// <summary>
    /// 发表文章
    /// </summary>
    [Post("/api/v1/posts")]
    Task<BlogPost> Add([Body] PostCreationDto dto);
    
    /// <summary>
    /// 获取文章详情
    /// </summary>
    [Get("/api/v1/posts/{id}")]
    Task<BlogPost> Get(string id);

    /// <summary>
    /// 更新文章
    /// </summary>
    [Put("/api/v1/posts/{id}")]
    Task<BlogPost> Update(string id, [Body] PostUpdateDto dto);

    /// <summary>
    /// 删除文章
    /// </summary>
    [Delete("/api/v1/posts/{id}")]
    Task Delete(string id);

    /// <summary>
    /// 上传图片
    /// </summary>
    [Multipart]
    [Post("/api/v1/posts/{id}/images")]
    Task<UploadImageResult> UploadImage(string id, StreamPart file);
}
