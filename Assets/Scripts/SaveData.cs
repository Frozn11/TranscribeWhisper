using System.Collections.Generic;

[System.Serializable]
public class TranscriptionData {
    public string fileName;
    public string date;
    public string duration;
    public string transcriptionText;
    public string originalPath;
}

[System.Serializable]
public class SaveData {
    public List<TranscriptionData> entries = new List<TranscriptionData>();
}