using System.Threading;
using System.Threading.Tasks;
using Refit;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Services.StarBlogApi;

public interface ISite {
    [Get("/api/v1/site/photos/random")]
    Task<SitePhoto> GetRandomPhoto(CancellationToken cancellationToken = default);
}
