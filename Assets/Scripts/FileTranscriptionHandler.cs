using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SFB; 
using Whisper;
using Novacode; 
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using UnityEngine.Networking;
using System.Threading.Tasks;
using System.IO;
using TMPro;

public class FileTranscriptionHandler : MonoBehaviour {
    [Header("Whisper Reference")] public WhisperManager whisperManager;

    [Header("UI Controls")] 
    public Button exportFileButton;
    public Button TranscribeButton;
    public Button deleteButton;
    [Header("UI Settings")] 
    public Button selectFileButton;
    public TMP_Text textFilePath;
    public TMP_Dropdown dropdownLanguage;
    public TMP_Dropdown dropdownModel;
    public Toggle toggleGPU;

    [Space] public GameObject buttonTemplate;
    public Transform recentFileList;
    public int number;
    public List<GameObject> buttonsCollections = new List<GameObject>();

    static string BaseSavePath => 
        Path.Combine(Application.persistentDataPath, "Saves");
    
    private string StuffFilePath => Path.Combine(BaseSavePath, "stuff.lul");

    string currentTranscription;

    private string selectedPath;
    
    private string selectedModelPath;

    private string modelPath = $"{Application.streamingAssetsPath}/Whisper";
    
    public static FileTranscriptionHandler instance { get; private set; }

    void Awake() {
        instance = this;
        whisperManager.OnProgress += OnProgressHandler;
    }

    private void Start() {
        buttonTemplate.SetActive(false);
        LoadFiles();
        ListModel();
    
        // Ensure the manager knows the initial toggle state without reloading
        whisperManager.useGpu = toggleGPU.isOn; 
    
        selectFileButton.onClick.AddListener(OpenFileBrowser);
        if (exportFileButton != null) exportFileButton.interactable = false;
        if (TranscribeButton != null) TranscribeButton.interactable = false;
    }
    public void OpenFileBrowser() {
        var extensions = new[] { new ExtensionFilter("Audio Files", "mp3", "wav", "ogg", "m4a") };
        var paths = StandaloneFileBrowser.OpenFilePanel("Select File", "", extensions, false);

        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0])) {
            //StartCoroutine(LoadAndTranscribe(paths[0]));
            selectedPath = paths[0];
            textFilePath.text = selectedPath;
            TranscribeButton.interactable = true;
        }
    }

    public void StartTranscribe() {
        StartCoroutine(LoadAndTranscribe(selectedPath));
    }


    private IEnumerator LoadAndTranscribe(string path) {

        //Make a copy of the button
        GameObject inst = Instantiate(buttonTemplate, recentFileList);

        buttonsCollections.Add(inst);

        //Getting the ButtonLogic.cs for created Instantiate
        ButtonLogic buttonLogic = inst.GetComponent<ButtonLogic>();
        
        buttonLogic.openTranscriptionThreeDotButton.interactable = false;
        buttonLogic.exportThreeDotButton.interactable = false;
        buttonLogic.renameThreeDotButton.interactable = false;

        inst.transform.SetSiblingIndex(1);


        buttonLogic.progress.text = "Loading file...";

        // Use AudioType.UNKNOWN to let Unity try to guess the format from the extension
        using (UnityWebRequest uwr = UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.UNKNOWN)) {
            yield return uwr.SendWebRequest();

            if (uwr.result != UnityWebRequest.Result.Success) {
                buttonLogic.progress.text = "Error: " + uwr.error;
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(uwr);

            //Getting the name of the file
            string nameFile = Path.GetFileNameWithoutExtension(selectedPath);

            //Setting the name of the button display to text
            buttonLogic.fileNameText.text = nameFile;
            //Getting the time right now and setting it to display to text
            buttonLogic.date.text =
                DateTime.Now.ToString("MMM dd, yyyy h:mm:ss tt", CultureInfo.GetCultureInfo("en-US"));

            float durationSeconds = clip.length;
            //Using TimeSpan for easy formating
            TimeSpan time = TimeSpan.FromSeconds(durationSeconds);
            string convertedDuration = "";

            if (durationSeconds < 60) {
                convertedDuration = string.Format("{0:D2}s", time.Seconds);
            }
            else if (time.TotalMinutes < 60) {
                convertedDuration = string.Format("{0:D2}m {1:D2}s", time.Minutes, time.Seconds);
            }
            else {
                convertedDuration = string.Format("{0:D2}h {1:D2}m", (int)time.TotalHours, time.Minutes);
            }

            buttonLogic.button.interactable = false;

            //Setting date for display to text
            buttonLogic.duration.text = convertedDuration;

            inst.SetActive(true);

            //buttonLogic.progress.text = "Transcribing...";
            RunWhisper(clip, buttonLogic);
        }
    }

    private async void RunWhisper(AudioClip clip, ButtonLogic buttonLogic) {
        var token = buttonLogic.cts.Token;

        try {
            var res = await whisperManager.GetTextAsync(clip, token);

            if (token.IsCancellationRequested) {
                Debug.Log("Transcription was cancelled. Cleaning up...");
                return; 
            }

            if (res != null) {
                string resText = res.Result.TrimStart();
                currentTranscription = resText;

                buttonLogic.progress.text = "Done";

                if (exportFileButton != null) exportFileButton.interactable = true;

                //makes the buttons interactable
                buttonLogic.button.interactable = true;
                buttonLogic.openTranscriptionThreeDotButton.interactable = true;
                buttonLogic.renameThreeDotButton.interactable = true;
                buttonLogic.exportThreeDotButton.interactable = true;

                //gives for ButtonLogic result of current transcription
                buttonLogic.transcribe = resText;
                SaveFiles();

            }
        }
        catch (OperationCanceledException) {
            Debug.Log("Transcription stopped because the user deleted the file.");
        }
        catch (Exception e) {
            Debug.LogError($"Whisper Error: {e.Message}");
        }
    }

    private void OnProgressHandler(int progress) {
        foreach(var btn in buttonsCollections) {
            if(btn != null) {
                var logic = btn.GetComponent<ButtonLogic>();
                if(logic.progress.text != "Done" && logic.progress.text != "Error")
                    logic.progress.text = $"{progress}%";
            }
        }
    }

    public void SaveTranscription(string transcription) {
        if (string.IsNullOrEmpty(transcription)) {
            Debug.Log("Goback"); 
            return;
        }

        var extensions = new[] {
            new ExtensionFilter("Text File", "txt"),
            new ExtensionFilter("Word Document", "docx"),
            new ExtensionFilter("Portable Document Format", "pdf"),
        };

        var path = StandaloneFileBrowser.SaveFilePanel("Save File", "", "Transcription", extensions);

        if (string.IsNullOrEmpty(path)) return;

        string extension = Path.GetExtension(path).ToLower();

        try {
            if (extension == ".docx") {
                SaveAsDocx(path, transcription);
            }
            else if (extension == ".pdf") {
                SaveAsPdf(path, transcription);
            }
            else {
                File.WriteAllText(path, transcription);
            }

            Debug.Log("File Saved Successfully!");
        }
        catch (Exception e) {
            Debug.LogError($"Save Failed: {e.Message}");
        }
    }

    // btnLogic for Native Word Files
    private void SaveAsDocx(string path, string text) {
        // Use DocX specifically to avoid confusion
        using (DocX document = DocX.Create(path)) {
            document.InsertParagraph("Transcription Report").Bold().FontSize(18).Alignment = Alignment.center;
            document.InsertParagraph(System.DateTime.Now.ToString("f")).Italic().Alignment = Alignment.center;
            document.InsertParagraph("\n" + text);
            document.Save();
        }
    }

    // btnLogic for PDF Files
    private void SaveAsPdf(string path, string text) {
        try {
            //Create the document
            PdfDocument document = new PdfDocument();
            PdfPage page = document.AddPage();
            XGraphics gfx = XGraphics.FromPdfPage(page);

            //Define Fonts (1.50 uses XFontStyle, not XFontStyleEx)
            XFont titleFont = new XFont("Arial", 18, XFontStyle.Bold);
            XFont bodyFont = new XFont("Arial", 11, XFontStyle.Regular);

            //Draw Header
            gfx.DrawString("Transcription Report", titleFont, XBrushes.Black,
                new XRect(0, 40, page.Width, 40), XStringFormats.Center);

            //Content
            XTextFormatter tf = new XTextFormatter(gfx);
            XRect rect = new XRect(40, 100, page.Width - 80, page.Height - 140);
            tf.DrawString(text, bodyFont, XBrushes.Black, rect);

            //Save
            document.Save(path);
            Debug.Log("PDF Saved successfully with PDFSharp 1.50!");
        }
        catch (Exception e) {
            Debug.LogError("PDF Error: " + e.Message);
        }
    }

    private void OnDestroy() {
        // Clean up event subscription when object is destroyed
        if (whisperManager != null) {
            whisperManager.OnProgress -= OnProgressHandler;
        }
    }

    public void SaveFiles() {
        try {
            if (!Directory.Exists(BaseSavePath)) Directory.CreateDirectory(BaseSavePath);

            SaveData data = new SaveData();
            foreach (GameObject btnObj in buttonsCollections) {
                if (btnObj == null) continue;
                ButtonLogic btnLogic = btnObj.GetComponent<ButtonLogic>();
                
                data.entries.Add(new TranscriptionData {
                    fileName = btnLogic.fileNameText.text,
                    date = btnLogic.date.text,
                    duration = btnLogic.duration.text,
                    transcriptionText = btnLogic.transcribe
                });
            }
            
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(StuffFilePath, json);
            Debug.Log("File Saved Successfully!");
        }
        catch (Exception e) {
            Debug.LogError($"Save Failed: {e.Message}");
        }
    }

    public void LoadFiles() {
        if (!File.Exists(StuffFilePath)) return;

        try {
            string json = File.ReadAllText(StuffFilePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            foreach (var entry in data.entries) {
                GameObject inst = Instantiate(buttonTemplate, recentFileList);
                buttonsCollections.Add(inst);

                ButtonLogic btnLogic = inst.GetComponent<ButtonLogic>();

                btnLogic.fileNameText.text = entry.fileName;
                btnLogic.date.text = entry.date;
                btnLogic.duration.text = entry.duration;
                btnLogic.transcribe = entry.transcriptionText;


                btnLogic.button.interactable = true;
                btnLogic.openTranscriptionThreeDotButton.interactable = true;
                btnLogic.exportThreeDotButton.interactable = true;
                btnLogic.renameThreeDotButton.interactable = true;
                btnLogic.deleteThreeDotButton.interactable = true;
                TranscribeButton.interactable = true;
                btnLogic.progress.text = "Done";
                
                exportFileButton.interactable = true;

                inst.SetActive(true);

            }

            Debug.Log("All done and loaded!");
        }
        catch (Exception e) {
            Debug.LogError($"Load Failed: {e.Message}");
        }
    }

    public void ChangeModel(int stuff) {
        switch (stuff) {
            case 0:
                Debug.Log("ggml-tiny");
                break;
        }
    }

    public void ListModel() {
        string path = $"{Application.streamingAssetsPath}/Whisper" ;
        
        string [] files = Directory.GetFiles(path, "*bin");
        foreach (string file in files) {
            Debug.Log($"Found file: {Path.GetFileName(file)}");
        }
    }
    
    public void ChangeLanguage(int stuff) {
        switch (stuff) {
            case 0:
                Debug.Log("Auto");
                break;
            case 1:
                Debug.Log("RU");
                break;
            case 2:
                Debug.Log("EN");
                break;
            case 3:
                Debug.Log("DE");
                break;
        }
        
    }
    public async void UseGPU(bool isOn) {
        // Disable the toggle so the user can't spam it while the model reloads
        toggleGPU.interactable = false;

        Debug.Log($"Switching GPU to {isOn}. This may take a few seconds...");
    
        // Call the improved method in WhisperManager
        await Task.Run(() => whisperManager.SetGpu(isOn)); 

        // Re-enable the toggle once the model is ready
        toggleGPU.interactable = true;
    }

}


