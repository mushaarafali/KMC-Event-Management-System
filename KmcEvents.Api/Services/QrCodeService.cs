using QRCoder;

namespace KmcEvents.Api.Services;

public class QrCodeService
{
    public byte[] GenerateBytes(string text)
    {
        using var generator = new QRCodeGenerator();

        using var data = generator.CreateQrCode(
            text,
            QRCodeGenerator.ECCLevel.Q
        );

        var qrCode = new PngByteQRCode(data);

        return qrCode.GetGraphic(12);
    }

    public string GenerateDataUrl(string text)
    {
        var bytes = GenerateBytes(text);

        var base64 = Convert.ToBase64String(bytes);

        return $"data:image/png;base64,{base64}";
    }
}