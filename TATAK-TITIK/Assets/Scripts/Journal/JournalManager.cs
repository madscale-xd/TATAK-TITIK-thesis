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
        // Calculate how many *pairs* of pages are needed
        int totalPagePairs = Mathf.CeilToInt(entries.Count / (float)(maxEntriesPerPage * 2));
        int totalPages = totalPagePairs * 2; // Always even

        // Clamp currentPageIndex to valid range
        currentPageIndex = Mathf.Clamp(currentPageIndex, 0, Mathf.Max(0, totalPages - 2));

        // Clear old pages
        foreach (var page in pages)
            Destroy(page);
        pages.Clear();

        // Rebuild pages
        for (int pageIndex = 0; pageIndex < totalPages; pageIndex++)
        {
            GameObject page = Instantiate(pagePrefab, pagesParent);
            page.SetActive(false);
            pages.Add(page);

            JournalPage jp = page.GetComponent<JournalPage>();
            bool hasEntriesThisPage = false;

            for (int i = 0; i < maxEntriesPerPage; i++)
            {
                int entryIndex = pageIndex * maxEntriesPerPage + i;
                if (entryIndex >= entries.Count) break;

                hasEntriesThisPage = true;

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

            if (hasEntriesThisPage && jp != null)
            {
                jp.DisablePlaceholderIfNeeded();
            }
        }

        // Enable only the two visible pages
        for (int i = 0; i < pages.Count; i++)
        {
            bool shouldBeActive = (i == currentPageIndex || i == currentPageIndex + 1);
            pages[i].SetActive(shouldBeActive);
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
