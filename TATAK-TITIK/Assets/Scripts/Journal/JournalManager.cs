using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class JournalManager : MonoBehaviour
{
    public static JournalManager Instance;
    private SceneButtonManager SBM;

    [Header("Prefabs & Containers")]
    public GameObject entrySlotPrefab; // Prefab of the journal entry slot UI (Text + InputField)
    public GameObject pagePrefab;       // Prefab of the page container with Grid Layout Group (3 columns)
    public Transform pagesParent;       // Parent transform for pages in UI

    private List<JournalEntry> entries = new List<JournalEntry>();
    private List<GameObject> pages = new List<GameObject>();

    private int maxEntriesPerPage = 4;
    private int currentPageIndex = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        SaveLoadManager slm = FindObjectOfType<SaveLoadManager>();
        if (slm != null && SaveLoadManager.pendingJournalEntries != null)
        {
            LoadEntries(SaveLoadManager.pendingJournalEntries);
            SaveLoadManager.pendingJournalEntries = null; // Clear after applying
        }
    }

    private void Update()
    {
        SceneButtonManager sbm = FindObjectOfType<SceneButtonManager>();
        if (sbm.IsJKeyEnabled() && Input.GetKeyDown(KeyCode.J))
        {
            sbm.ToggleJournalPanelJ();
        }
    }

    /// <summary>
    /// Adds a new journal entry by keyword, if not already added.
    /// </summary>
    public void AddEntry(string keyword, string displayWord)
    {
        if (entries.Exists(e => e.key == keyword))
            return;

        entries.Add(new JournalEntry(keyword, displayWord));
        RefreshJournalUI();
    }

    /// <summary>
    /// Rebuilds all pages and slots according to current entries.
    /// </summary>
    private void RefreshJournalUI()
    {
        // Reset page index to 0 so we always start at the first page
        currentPageIndex = 0;

        // Destroy existing pages UI
        foreach (var page in pages)
            Destroy(page);
        pages.Clear();

        // Calculate total pages needed
        int totalPages = Mathf.CeilToInt(entries.Count / (float)maxEntriesPerPage);

        for (int pageIndex = 0; pageIndex < totalPages; pageIndex++)
        {
            GameObject page = Instantiate(pagePrefab, pagesParent);
            page.SetActive(false); // Initially deactivate all pages
            pages.Add(page);

            for (int i = 0; i < maxEntriesPerPage; i++)
            {
                int entryIndex = pageIndex * maxEntriesPerPage + i;
                if (entryIndex >= entries.Count) break;

                GameObject slotGO = Instantiate(entrySlotPrefab, page.transform);
                TMP_Text label = slotGO.transform.Find("Label").GetComponent<TMP_Text>();
                TMP_InputField inputField = slotGO.transform.Find("InputField").GetComponent<TMP_InputField>();

                label.text = entries[entryIndex].displayWord;
                inputField.text = entries[entryIndex].playerNote;
                inputField.interactable = true;
                inputField.placeholder.GetComponent<TMP_Text>().text = "?";

                int capturedIndex = entryIndex;
                inputField.onValueChanged.AddListener((string val) =>
                {
                    entries[capturedIndex].playerNote = val;
                });
            }
        }
        for (int i = 0; i < pages.Count; i++)
        {
            pages[i].SetActive(i == currentPageIndex || i == currentPageIndex + 1);
        }
    }

    /// <summary>
    /// Shows the next page if possible.
    /// </summary>
    public void NextPage()
    {
        if (currentPageIndex + 2 < pages.Count)
        {
            currentPageIndex += 2;
            RefreshJournalUI();
        }
    }

    public void PreviousPage()
    {
        if (currentPageIndex - 2 >= 0)
        {
            currentPageIndex -= 2;
            RefreshJournalUI();
        }
    }

    public List<JournalEntry> GetEntries()
    {
        return new List<JournalEntry>(entries);
    }

    public void LoadEntries(List<JournalEntry> loadedEntries)
    {
        entries = loadedEntries ?? new List<JournalEntry>();
        RefreshJournalUI();
    }
}
