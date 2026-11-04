using System;
using System.Threading;
using System.Threading.Tasks;

namespace StarBlogPublisher.Services.Application;

public class SitePhotoApplicationService(ApiService api) {
    public async Task<Uri> GetRandomImageUriAsync(CancellationToken cancellationToken = default) {
        try {
            var photo = await api.Site.GetRandomPhoto(cancellationToken);
            if (string.IsNullOrWhiteSpace(photo?.FilePath)) throw new InvalidOperationException("站点未返回图片地址");
            var baseUri = new Uri(api.BaseUrl.TrimEnd('/') + "/");
            // PhotoResponse.FilePath is relative to the photography media directory, not the site root.
            return new Uri(baseUri, "media/photography/" + photo.FilePath.Replace('\\', '/').TrimStart('/'));
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            throw new InvalidOperationException($"获取站点随机图片失败: {ApiError.Describe(ex)}", ex);
        }
    }
}
