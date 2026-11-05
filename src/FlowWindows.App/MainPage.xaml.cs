using System;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using FlowWindows.Core.Interfaces;
using FlowWindows_App.Pages;

namespace FlowWindows_App;

public sealed partial class MainPage : Page
{
    private ITimerEngine? _timerEngine;
    private IRepository? _repository;
    private IAnalyticsService? _analyticsService;
    private IStudyPlannerService? _studyPlannerService;
    private ITaskService? _taskService;
    private IGoalsService? _goalsService;
    private IAudioNotificationService? _audioService;
    private IReportService? _reportService;
    private Action? _toggleMiniTimerAction;

    private TimerPage? _timerPage;
    private StatisticsPage? _statisticsPage;
    private TasksPage? _tasksPage;
    private StudyPlannerPage? _studyPlannerPage;
    private InsightsPage? _insightsPage;
    private SettingsPage? _settingsPage;

    public MainPage()
    {
        InitializeComponent();
    }

    public void InitializeServices(
        ITimerEngine timerEngine,
        IRepository repository,
        IAnalyticsService analyticsService,
        IStudyPlannerService studyPlannerService,
        ITaskService taskService,
        IGoalsService goalsService,
        IAudioNotificationService audioService,
        IReportService reportService,
        Action toggleMiniTimerAction)
    {
        _timerEngine = timerEngine;
        _repository = repository;
        _analyticsService = analyticsService;
        _studyPlannerService = studyPlannerService;
        _taskService = taskService;
        _goalsService = goalsService;
        _audioService = audioService;
        _reportService = reportService;
        _toggleMiniTimerAction = toggleMiniTimerAction;

        NavigateToTimer();
    }

    public void NavigateToTimer()
    {
        if (_timerEngine == null || _repository == null) return;

        _timerPage ??= new TimerPage();
        _timerPage.Initialize(
            _timerEngine,
            _repository,
            navigateToStatistics: () => NavigateTo("Statistics"),
            navigateToTasks: () => NavigateTo("Tasks"),
            navigateToStudyPlanner: () => NavigateTo("StudyPlanner"),
            navigateToInsights: () => NavigateTo("Insights"),
            navigateToSettings: () => NavigateTo("Settings"),
            toggleMiniTimer: _toggleMiniTimerAction ?? (() => { })
        );
        ContentFrame.Content = _timerPage;
    }

    public void NavigateTo(string target)
    {
        if (_timerEngine == null || _repository == null) return;

        switch (target)
        {
            case "Timer":
                NavigateToTimer();
                break;

            case "Statistics":
                _statisticsPage ??= new StatisticsPage();
                if (_analyticsService != null)
                {
                    _statisticsPage.Initialize(_repository, _analyticsService, NavigateToTimer);
                }
                ContentFrame.Content = _statisticsPage;
                break;

            case "Tasks":
                _tasksPage ??= new TasksPage();
                if (_taskService != null)
                {
                    _tasksPage.Initialize(_taskService, _timerEngine, NavigateToTimer);
                }
                ContentFrame.Content = _tasksPage;
                break;

            case "StudyPlanner":
                _studyPlannerPage ??= new StudyPlannerPage();
                if (_studyPlannerService != null)
                {
                    _studyPlannerPage.Initialize(_studyPlannerService, _timerEngine, NavigateToTimer);
                }
                ContentFrame.Content = _studyPlannerPage;
                break;

            case "Insights":
                _insightsPage ??= new InsightsPage();
                if (_analyticsService != null && _goalsService != null)
                {
                    _insightsPage.Initialize(_analyticsService, _goalsService, NavigateToTimer);
                }
                ContentFrame.Content = _insightsPage;
                break;

            case "Settings":
                _settingsPage ??= new SettingsPage();
                if (_audioService != null && _reportService != null)
                {
                    _settingsPage.Initialize(_repository, _timerEngine, _audioService, _reportService, NavigateToTimer);
                }
                ContentFrame.Content = _settingsPage;
                break;
        }
    }
}
