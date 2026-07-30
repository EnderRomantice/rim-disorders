using Verse;

namespace MoreMentalDisorders
{
    public static class MMDLocalization
    {
        public static bool English
        {
            get
            {
                return LanguageDatabase.activeLanguage != null
                    && LanguageDatabase.activeLanguage.folderName == "English";
            }
        }

        public static string Pick(string chinese, string english)
        {
            return English ? english : chinese;
        }
    }
}
