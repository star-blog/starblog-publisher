using System.Threading.Tasks;
using StarBlogPublisher.Models;
using Refit;

using LoginResp = CodeLab.Share.ViewModels.Response.ApiResponse<StarBlogPublisher.Models.LoginToken>;

namespace StarBlogPublisher.Services.StarBlogApi;

public interface IAuth {
    [Post("/Api/Auth/Login")]
    Task<LoginResp> Login([Body] LoginUser login);
}