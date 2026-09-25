using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace Inventory_Management_System.BusinessLogic
{
    /// <summary>
    /// Service for managing physical product image storage and retrieval.
    /// Handles unique GUID-based filenames, decoupled non-locking MemoryStreams,
    /// and consistent modern UI placeholders.
    /// </summary>
    public class ImageService
    {
        private static readonly Lazy<ImageService> _instance = new(() => new ImageService());
        public static ImageService Instance => _instance.Value;

        public static string UploadsDirectory => Path.Combine(Application.StartupPath, "Uploads", "Products");

        public ImageService()
        {
            EnsureUploadsDirectoryExists();
        }

        /// <summary>
        /// Ensures the application-relative storage folder exists.
        /// </summary>
        public void EnsureUploadsDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(UploadsDirectory))
                {
                    Directory.CreateDirectory(UploadsDirectory);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create uploads directory: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves an external image file to the application-relative Uploads/Products folder
        /// with a collision-resistant unique name (e.g., PRD_f47ac10b.png).
        /// Returns the relative file name stored in the database.
        /// </summary>
        public string SaveImage(string sourceFilePath)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath))
                throw new ArgumentException("Source image path cannot be empty.", nameof(sourceFilePath));

            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException("Source image file was not found.", sourceFilePath);

            EnsureUploadsDirectoryExists();

            string extension = Path.GetExtension(sourceFilePath).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension))
            {
                extension = ".png";
            }

            // Generate unique filename: PRD_f47ac10b.ext
            string uniqueToken = Guid.NewGuid().ToString("N")[..8];
            string targetFileName = $"PRD_{uniqueToken}{extension}";
            string destinationPath = Path.Combine(UploadsDirectory, targetFileName);

            // Copy file safely using non-locking byte streaming
            byte[] fileBytes = File.ReadAllBytes(sourceFilePath);
            File.WriteAllBytes(destinationPath, fileBytes);

            return targetFileName;
        }

        /// <summary>
        /// Loads an image from the Uploads/Products directory using an in-memory stream
        /// to ensure the physical file is NEVER locked by the process.
        /// Returns a clean default placeholder if the file is missing or invalid.
        /// </summary>
        public Image LoadImage(string? fileName)
        {
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                string fullPath = Path.Combine(UploadsDirectory, fileName);
                if (File.Exists(fullPath))
                {
                    try
                    {
                        byte[] bytes = File.ReadAllBytes(fullPath);
                        using var ms = new MemoryStream(bytes);
                        using var tempImg = Image.FromStream(ms);
                        // Return deep clone of bitmap to avoid GDI+ lock or disposal issues
                        return new Bitmap(tempImg);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading image '{fileName}': {ex.Message}");
                    }
                }
            }

            return GetDefaultPlaceholder(150, 150);
        }

        /// <summary>
        /// Loads or resizes a neat 40x40 thumbnail image for DataGridView cells,
        /// ensuring optimal UI scrolling performance and memory footprint.
        /// </summary>
        public Image LoadThumbnail(string? fileName, int width = 40, int height = 40)
        {
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                string fullPath = Path.Combine(UploadsDirectory, fileName);
                if (File.Exists(fullPath))
                {
                    try
                    {
                        byte[] bytes = File.ReadAllBytes(fullPath);
                        using var ms = new MemoryStream(bytes);
                        using var original = Image.FromStream(ms);

                        var thumb = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                        using (var g = Graphics.FromImage(thumb))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = SmoothingMode.HighQuality;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.Clear(Color.White);

                            // Calculate aspect ratio fit within bounds
                            float ratioX = (float)width / original.Width;
                            float ratioY = (float)height / original.Height;
                            float ratio = Math.Min(ratioX, ratioY);

                            int newWidth = (int)(original.Width * ratio);
                            int newHeight = (int)(original.Height * ratio);
                            int posX = (width - newWidth) / 2;
                            int posY = (height - newHeight) / 2;

                            g.DrawImage(original, posX, posY, newWidth, newHeight);

                            // Subtle border around thumbnail
                            using var borderPen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f);
                            g.DrawRectangle(borderPen, 0, 0, width - 1, height - 1);
                        }
                        return thumb;
                    }
                    catch
                    {
                        // Fall through to placeholder thumbnail
                    }
                }
            }

            return GetDefaultPlaceholder(width, height);
        }

        /// <summary>
        /// Deletes the physical image file from the Uploads/Products folder.
        /// </summary>
        public void DeleteImage(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return;

            try
            {
                string fullPath = Path.Combine(UploadsDirectory, fileName);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to delete image '{fileName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a clean, modern placeholder bitmap for items without a custom image.
        /// </summary>
        public Image GetDefaultPlaceholder(int width = 150, int height = 150)
        {
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            // Soft slate background
            var bgBrush = new SolidBrush(ColorTranslator.FromHtml("#F8FAFC"));
            g.FillRectangle(bgBrush, 0, 0, width, height);

            // Subtle border
            using (var borderPen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f))
            {
                g.DrawRectangle(borderPen, 0, 0, width - 1, height - 1);
            }

            // Draw modern minimalist package/box symbol
            if (width >= 60)
            {
                using var iconFont = new Font("Segoe UI Emoji", Math.Max(18f, width / 4.5f));
                using var textBrush = new SolidBrush(ColorTranslator.FromHtml("#94A3B8"));
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString("📦", iconFont, textBrush, new RectangleF(0, 0, width, height * 0.75f), sf);

                using var subFont = new Font("Segoe UI", Math.Max(7.5f, width / 18f), FontStyle.Regular);
                g.DrawString("No Image", subFont, textBrush, new RectangleF(0, height * 0.65f, width, height * 0.3f), sf);
            }
            else
            {
                // Compact icon for small grid thumbnails
                using var iconFont = new Font("Segoe UI Emoji", Math.Max(12f, width / 2.5f));
                using var textBrush = new SolidBrush(ColorTranslator.FromHtml("#94A3B8"));
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString("📦", iconFont, textBrush, new RectangleF(0, 0, width, height), sf);
            }

            return bmp;
        }
    }
}
