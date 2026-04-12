using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PS5_Controller_Tools.About
{
    public partial class AboutWindow : Window
    {
        public AboutWindow(AboutWindowContent content)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            InitializeComponent();
            ApplyContent(content);
        }

        private void ApplyContent(AboutWindowContent content)
        {
            ApplicationNameText.Text = content.ApplicationName;
            VersionText.Text = content.VersionText;
            VersionNum.Text = content.VersionNum;

            DeveloperTitleText.Text = content.DeveloperTitle;
            DeveloperValueText.Text = content.DeveloperValue;

            DescriptionTitleText.Text = content.DescriptionTitle;
            DescriptionValueText.Text = content.DescriptionValue;

            EmailTitleText.Text = content.EmailTitle;
            EmailValueText.Text = content.EmailValue;

            ImageSource? backgroundSource = TryLoadImage(content.BackgroundImageRelativePath);
            if (backgroundSource != null)
            {
                BackgroundVisual.Source = backgroundSource;
                BackgroundVisual.Visibility = Visibility.Visible;
            }

            ImageSource? logoSource = TryLoadImage(content.LogoImageRelativePath)
                                   ?? TryLoadImage("Assets/KB13Tran.png");
            if (logoSource != null)
            {
                LogoVisual.Source = logoSource;
                LogoVisual.Visibility = Visibility.Visible;
            }
        }

        private static ImageSource? TryLoadImage(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return null;

            foreach (Uri uri in EnumerateCandidateUris(relativePath))
            {
                ImageSource? image = TryCreateImage(uri);
                if (image != null)
                    return image;
            }

            return null;
        }

        private static IEnumerable<Uri> EnumerateCandidateUris(string relativePath)
        {
            yield return new Uri($"pack://application:,,,/{relativePath}", UriKind.Absolute);

            string normalizedRelativePath = relativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            string[] candidatePaths =
            {
                Path.Combine(AppContext.BaseDirectory, normalizedRelativePath),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", normalizedRelativePath)
            };

            foreach (string candidatePath in candidatePaths.Distinct())
            {
                string fullPath = Path.GetFullPath(candidatePath);
                if (File.Exists(fullPath))
                    yield return new Uri(fullPath, UriKind.Absolute);
            }
        }

        private static ImageSource? TryCreateImage(Uri uri)
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = uri;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }
    }
}
