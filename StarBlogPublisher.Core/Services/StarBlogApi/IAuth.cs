using System.Threading.Tasks;
using StarBlogPublisher.Models;
using Refit;
using StarBlogPublisher.Models.Dtos;

namespace StarBlogPublisher.Services.StarBlogApi;

public interface IAuth {
    [Post("/api/v1/auth/tokens")]
    Task<LoginToken> Login([Body] LoginUser login);
}
