using System.Threading.Tasks;
using Refit;
using StarBlogPublisher.Models;
using StarBlogPublisher.Models.Dtos;

namespace StarBlogPublisher.Services.StarBlogApi;

public interface IBlogPost {
    [Post("/Api/BlogPost")]
    Task<CodeLab.Share.ViewModels.Response.ApiResponse<BlogPost>> Add(PostCreationDto dto);
    
    [Multipart]
    [Post("/Api/BlogPost/{id}/UploadImage")]
    Task<CodeLab.Share.ViewModels.Response.ApiResponse<BlogPost>> UploadImage(string id, StreamPart file);
}