using System;

namespace PS5_Controller_Tools.About
{
    public sealed class AboutWindowContent
    {
        public string ApplicationName { get; init; } = string.Empty;
        public string VersionText { get; init; } = string.Empty;
        public string VersionNum { get; init; } = string.Empty;

        public string DeveloperTitle { get; init; } = string.Empty;
        public string DeveloperValue { get; init; } = string.Empty;

        public string DescriptionTitle { get; init; } = string.Empty;
        public string DescriptionValue { get; init; } = string.Empty;

        public string EmailTitle { get; init; } = string.Empty;
        public string EmailValue { get; init; } = string.Empty;

        public string? BackgroundImageRelativePath { get; init; }
        public string? LogoImageRelativePath { get; init; }

        public static AboutWindowContent CreateDefault()
        {
            return new AboutWindowContent
            {
                ApplicationName = "PS5_Controller_Tools",
                VersionText = "Version :",
                VersionNum = "v1.0.0",

                DeveloperTitle = "Développé par :",
                DeveloperValue = "CGD KB13",

                DescriptionTitle = "Description :",
                DescriptionValue = "Outil de test pour manette PS5 après réparation.",

                EmailTitle = "Email de contact :",
                EmailValue = "christophe.kb13@gmail.com",

                BackgroundImageRelativePath = "Assets/KB13Contact.jpg",
                LogoImageRelativePath = "Assets/KB13Tran.jpg"
            };
        }
    }
}