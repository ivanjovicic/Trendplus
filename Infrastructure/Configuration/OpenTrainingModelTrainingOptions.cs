namespace Infrastructure.Configuration;

public sealed class OpenTrainingModelTrainingOptions
{
    public const string Section = "OpenTrainingModelTraining";

    /// <summary>Allows disabling this worker even when the global workers switch is enabled.</summary>
    public bool Enabled { get; set; } = true;

    public int StartupDelaySeconds { get; set; } = 30;
    public int PollSeconds { get; set; } = 20;
    public int PauseCheckSeconds { get; set; } = 5;

    /// <summary>Python executable (e.g. "python", "python3", or absolute path).</summary>
    public string PythonExe { get; set; } = "python";

    /// <summary>Training script path. Can be relative to app base directory.</summary>
    public string TrainingScriptPath { get; set; } = "Python/training/train_sell_probability_rs.py";

    /// <summary>Where to write artifacts (ONNX + JSON). Can be relative.</summary>
    public string OutputDir { get; set; } = "out/models/open_training";

    /// <summary>Safety cap for export rows.</summary>
    public int Take { get; set; } = 500_000;

    /// <summary>If true, activates the new model_version after a successful run.</summary>
    public bool ActivateOnSuccess { get; set; } = true;
}

