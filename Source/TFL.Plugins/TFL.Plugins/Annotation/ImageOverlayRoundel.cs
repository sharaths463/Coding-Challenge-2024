using Microsoft.Xrm.Sdk;
using System;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using Microsoft.Xrm.Sdk.Query;
using System.Drawing.Drawing2D;
using System.Linq;

namespace TFL.Plugins.Annotation
{
    public class ImageOverlayRoundel : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity annotation)
            {
                if (annotation.LogicalName == "annotation" &&
                    annotation.Contains("documentbody") &&
                    annotation.Contains("mimetype") &&
                    annotation.Contains("objectid") &&
                    annotation["objectid"] is EntityReference objectId &&
                    objectId.LogicalName == "tfl_observation")
                {
                    var mimeType = annotation["mimetype"].ToString();

                    if (mimeType.StartsWith("image/"))
                    {
                        var base64ImageString = annotation["documentbody"].ToString();
                        var imageBytes = Convert.FromBase64String(base64ImageString);

                        try
                        {
                            var modifiedImageBytes = OverlayRoundel(imageBytes, mimeType);
                            var updatedBase64Image = Convert.ToBase64String(modifiedImageBytes);
                            annotation["documentbody"] = updatedBase64Image;
                            context.InputParameters["Target"] = annotation;
                        }
                        catch (Exception ex)
                        {
                            var imageSize = imageBytes.Length; // Get size in bytes
                            throw new InvalidPluginExecutionException($"Error processing image: {ex.Message}. Image Size: {imageSize} bytes");
                        }
                    }
                }
            }
        }

        public byte[] OverlayRoundel(byte[] imageBytes, string mimeType)
        {
            string tempInputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
            string tempOutputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jpg"); // Save as JPG to reduce size

            try
            {
                // Save original image to a temporary file
                File.WriteAllBytes(tempInputPath, imageBytes);

                using (var originalBitmap = new Bitmap(tempInputPath))
                {
                    // Resize image if necessary
                    const int maxDimension = 640; // Further reduce max dimension for better memory management
                    Bitmap targetBitmap;

                    if (originalBitmap.Width > maxDimension || originalBitmap.Height > maxDimension)
                    {
                        float scalingFactor = Math.Min((float)maxDimension / originalBitmap.Width, (float)maxDimension / originalBitmap.Height);
                        int newWidth = (int)(originalBitmap.Width * scalingFactor);
                        int newHeight = (int)(originalBitmap.Height * scalingFactor);
                        targetBitmap = new Bitmap(originalBitmap, new Size(newWidth, newHeight));
                    }
                    else
                    {
                        targetBitmap = new Bitmap(originalBitmap);
                    }

                    using (targetBitmap)
                    using (var graphics = Graphics.FromImage(targetBitmap))
                    {
                        graphics.CompositingQuality = CompositingQuality.HighQuality;
                        graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

                        // Draw roundel
                        var roundelDiameter = Math.Min(targetBitmap.Width, targetBitmap.Height) / 15;
                        var roundelX = targetBitmap.Width - roundelDiameter - 20;
                        var roundelY = targetBitmap.Height - roundelDiameter - 20;
                        var roundelRectangle = new Rectangle(roundelX, roundelY, roundelDiameter, roundelDiameter);

                        DrawRoundel(graphics, roundelRectangle);
                        DrawCenteredRectangle(graphics, roundelRectangle);

                        // Save the modified image to a temporary output file with lower quality
                        var jpegQuality = 50L; // Set quality to 50 to further reduce size
                        var encoder = ImageCodecInfo.GetImageDecoders().First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
                        var encoderParams = new EncoderParameters(1);
                        encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, jpegQuality);
                        targetBitmap.Save(tempOutputPath, encoder, encoderParams);
                    }
                }

                // Read the processed image back into a byte array
                return File.ReadAllBytes(tempOutputPath);
            }
            finally
            {
                // Clean up temporary files
                if (File.Exists(tempInputPath)) File.Delete(tempInputPath);
                if (File.Exists(tempOutputPath)) File.Delete(tempOutputPath);
            }
        }

        private void DrawRoundel(Graphics g, Rectangle roundelRectangle)
        {
            using (var pen = new Pen(ColorTranslator.FromHtml("#233589"), 8))
            {
                g.DrawEllipse(pen, roundelRectangle);
            }
        }

        private void DrawCenteredRectangle(Graphics g, Rectangle roundelRectangle)
        {
            var rectangleWidth = roundelRectangle.Width + 20;
            var rectangleHeight = roundelRectangle.Height / 4;
            var rectangleX = roundelRectangle.X + (roundelRectangle.Width - rectangleWidth) / 2;
            var rectangleY = roundelRectangle.Y + (roundelRectangle.Height / 2) - (rectangleHeight / 2);

            using (var brush = new SolidBrush(ColorTranslator.FromHtml("#233589")))
            {
                var rectangle = new Rectangle(rectangleX, rectangleY, rectangleWidth, rectangleHeight);
                g.FillRectangle(brush, rectangle);
            }
        }
    }
}
