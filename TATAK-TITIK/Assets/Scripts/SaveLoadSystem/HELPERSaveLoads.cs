using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HELPERSaveLoads : MonoBehaviour
{
    private SaveLoadManager slm;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        slm = FindObjectOfType<SaveLoadManager>();
    }
    // Button helper methods
    public void SaveSlot1() => slm.SaveGame(1);
    public void LoadSlot1() => slm.LoadGame(1);

    public void SaveSlot2() => slm.SaveGame(2);
    public void LoadSlot2() => slm.LoadGame(2);

    public void SaveSlot3() => slm.SaveGame(3);
    public void LoadSlot3() => slm.LoadGame(3);

    public void SaveSlot4() => slm.SaveGame(4);
    public void LoadSlot4() => slm.LoadGame(4);

    public void SaveSlot5() => slm.SaveGame(5);
    public void LoadSlot5() => slm.LoadGame(5);

    public void ClearSlot1() => slm.ClearGame(1);
    public void ClearSlot2() => slm.ClearGame(2);
    public void ClearSlot3() => slm.ClearGame(3);
    public void ClearSlot4() => slm.ClearGame(4);
    public void ClearSlot5() => slm.ClearGame(5);
}
