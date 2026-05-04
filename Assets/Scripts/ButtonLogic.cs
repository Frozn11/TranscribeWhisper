using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ButtonLogic : MonoBehaviour {
    [Header("Button stuff")] 
    public Button button;
    public TMP_Text fileNameText;
    public TMP_Text date;
    public TMP_Text duration;
    public TMP_Text progress;
    public TMP_Text transcription;
    public GameObject panel;
    
    [Header("Three dots button references")]
    public Button openThreeDotButton;
    public Button openTranscriptionThreeDotButton;
    public Button exportThreeDotButton;
    public Button renameThreeDotButton;
    public Button deleteThreeDotButton;
    
    bool isOpenPanel = false;
    [Space()]
    
    [Header("Button Data")]
    public string transcribe;
    
    public CancellationTokenSource cts = new CancellationTokenSource();

    private void Awake() {
        panel.SetActive(false);

    }

    void Start() {
        button.onClick.AddListener(() => {
            InteractPanel(PanelAction.Close);;
        });
        openThreeDotButton.onClick.AddListener(() => {
            InteractPanel(PanelAction.Toggle);
        });
        openTranscriptionThreeDotButton.onClick.AddListener(() => {
            OpenTranscribe();
            InteractPanel(PanelAction.Close);;
        });
        exportThreeDotButton.onClick.AddListener(() => {
            FileTranscriptionHandler.instance.SaveTranscription(transcribe);
            InteractPanel(PanelAction.Close);;
        });
        renameThreeDotButton.onClick.AddListener(() => {
            Debug.Log("ahhh yes rename the file");
            InteractPanel(PanelAction.Close);;
        });
        deleteThreeDotButton.onClick.AddListener(() => {
            DeleteFile();
            InteractPanel(PanelAction.Close);;
        });
    }

    public void OpenTranscribe() {
        transcription.text = $"{fileNameText.text}\n<size=17.46><color=#050505>{date.text}</color></size>\n\n<size=23.1>{transcribe}</size>";
        InteractPanel(PanelAction.Close);;
        FileTranscriptionHandler.instance.exportFileButton.interactable = true;
        
        FileTranscriptionHandler.instance.exportFileButton.onClick.RemoveAllListeners();
        FileTranscriptionHandler.instance.exportFileButton.onClick.AddListener(() => {
            FileTranscriptionHandler.instance.SaveTranscription(transcribe);
        });
    }

    public enum PanelAction { Open, Close, Toggle }
    public void InteractPanel(PanelAction action) {
        switch (action) {
            case PanelAction.Open:
                isOpenPanel = true;
                break;
            case PanelAction.Close:
                isOpenPanel = false;
                break;
            case PanelAction.Toggle:
                isOpenPanel = !isOpenPanel;
                break;
        }
        panel.SetActive(isOpenPanel);
    }
    
    public void DeleteFile() {
        // Signal the transcription task to stop immediately
        if (cts != null) {
            cts.Cancel();
            cts.Dispose();
        }

        // Clean up the global list in the Handler
        if (FileTranscriptionHandler.instance.buttonsCollections.Contains(gameObject)) {
            FileTranscriptionHandler.instance.buttonsCollections.Remove(this.gameObject);
        }
        FileTranscriptionHandler.instance.SaveFiles();
        
        // 3. Destroy the UI object
        Destroy(this.gameObject);
        
        Debug.Log("File deleted and SaveData updated.");
    }

}
