using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StarBlogPublisher.Models;

/// <summary>
/// 窗口材质风格偏好。与 <see cref="ThemeMode"/> 独立，不参与浅/深色计算。
/// </summary>
[JsonConverter(typeof(InterfaceStyleJsonConverter))]
public enum InterfaceStyle {
    Standard = 0,
    Mica = 1,
    Acrylic = 2
}

public static class InterfaceStyleHelper {
    /// <summary>
    /// 缺失或未定义的取值按标准风格处理，避免错误值进入 GUI 材质应用。
    /// </summary>
    public static InterfaceStyle FromStorage(InterfaceStyle? style) {
        if (style is null) return InterfaceStyle.Standard;
        return Enum.IsDefined(style.Value) ? style.Value : InterfaceStyle.Standard;
    }
}

/// <summary>
/// 未知字符串或越界数值降为 <see cref="InterfaceStyle.Standard"/>，保持源生成 / AOT 兼容。
/// </summary>
public sealed class InterfaceStyleJsonConverter : JsonConverter<InterfaceStyle> {
    public override InterfaceStyle Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        switch (reader.TokenType) {
            case JsonTokenType.String:
                var text = reader.GetString();
                if (Enum.TryParse<InterfaceStyle>(text, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)) {
                    return parsed;
                }

                return InterfaceStyle.Standard;
            case JsonTokenType.Number:
                if (reader.TryGetInt32(out var number) && Enum.IsDefined(typeof(InterfaceStyle), number)) {
                    return (InterfaceStyle)number;
                }

                return InterfaceStyle.Standard;
            case JsonTokenType.Null:
                return InterfaceStyle.Standard;
            default:
                throw new JsonException($"无法解析界面风格：{reader.TokenType}。");
        }
    }

    public override void Write(Utf8JsonWriter writer, InterfaceStyle value, JsonSerializerOptions options) {
        writer.WriteStringValue(InterfaceStyleHelper.FromStorage(value).ToString());
    }
}
