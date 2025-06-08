[System.Serializable]
 public class JournalEntry
    {
        public string key;
        public string displayWord;
        public string playerNote;

        public JournalEntry(string key, string displayWord)
        {
            this.key = key;
            this.displayWord = displayWord;
            this.playerNote = "";
        }
    }
