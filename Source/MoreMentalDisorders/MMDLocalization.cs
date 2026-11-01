using Verse;

namespace MoreMentalDisorders
{
    public static class MMDLocalization
    {
        public static bool English
        {
            get
            {
                string folder = LanguageDatabase.activeLanguage?.folderName;
                return folder == null || !folder.StartsWith("Chinese", System.StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string Pick(string chinese, string english)
        {
            return English ? english : chinese;
        }
    }
}
