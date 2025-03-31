private async void SolveVoiceButton_Click(object sender, RoutedEventArgs e)
{
    if (string.IsNullOrWhiteSpace(TranscribedTextBox.Text) ||
        TranscribedTextBox.Text == "Transcribed text will appear here..." ||
        TranscribedTextBox.Text == "Recording..." ||
        TranscribedTextBox.Text == "Transcribing...")
    {
        MessageBox.Show("Please record and transcribe your problem first", "Input required",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
    }

    try
    {
        // Disable button during solving
        SolveVoiceButton.IsEnabled = false;
        StatusBarText.Text = "Solving problem...";

        // Пытаемся определить тип проблемы
        string problemType = DetectProblemType(TranscribedTextBox.Text);
        VoiceProblemTypeBox.Text = $"Detected Problem Type: {problemType}";
        VoiceProblemTypeBox.Visibility = Visibility.Visible;

        // Solve the problem
        _lastSolution = await _aiService.SolveProgrammingProblemAsync(
            TranscribedTextBox.Text, VoiceWithExplanationCheckBox.IsChecked ?? false);

        // Обновляем UI
        StatusBarText.Text = "Solution ready";

        // Показываем результат
        VoiceSolutionTextBox.Text = _lastSolution.Solution;
        VoiceSolutionTextBox.Visibility = Visibility.Visible;

        // Показываем решение
        _overlayService.ShowSolution(_lastSolution);
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[ERROR] Problem solving error: {ex.Message}");
        StatusBarText.Text = $"Error: {ex.Message}";
        
        // Очищаем предыдущие результаты
        VoiceSolutionTextBox.Text = "Failed to get solution";
        VoiceSolutionTextBox.Visibility = Visibility.Visible;
    }
    finally
    {
        // Re-enable button
        SolveVoiceButton.IsEnabled = true;
    }
}

// Метод для определения типа проблемы (примитивная версия)
private string DetectProblemType(string text)
{
    text = text.ToLower();

    // Простые правила определения типа задачи
    if (text.Contains("algorithm") || text.Contains("sort") || text.Contains("search"))
        return "Algorithmic Problem";
    
    if (text.Contains("class") || text.Contains("object") || text.Contains("inheritance"))
        return "Object-Oriented Programming";
    
    if (text.Contains("web") || text.Contains("http") || text.Contains("api"))
        return "Web Development";
    
    if (text.Contains("data") && (text.Contains("structure") || text.Contains("list")))
        return "Data Structures";
    
    if (text.Contains("recursive") || text.Contains("recursion"))
        return "Recursive Problem";
    
    if (text.Contains("dynamic") && text.Contains("programming"))
        return "Dynamic Programming";
    
    return "General Programming";
}