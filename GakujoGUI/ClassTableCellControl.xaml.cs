﻿using GakujoGUI.Models;
using NLog;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MessageBox = ModernWpf.MessageBox;

namespace GakujoGUI
{
    /// <summary>
    /// ClassTablesCell.xaml の相互作用ロジック
    /// </summary>
    public partial class ClassTableCellControl : UserControl
    {
        public ClassTableCellControl()
        {
            InitializeComponent();
        }

        public static readonly RoutedEvent ClassContactButtonClickEvent = EventManager.RegisterRoutedEvent("ClassContactButtonClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ClassTableCellControl));
        public static readonly RoutedEvent ReportButtonClickEvent = EventManager.RegisterRoutedEvent("ReportButtonClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ClassTableCellControl));
        public static readonly RoutedEvent QuizButtonClickEvent = EventManager.RegisterRoutedEvent("QuizButtonClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ClassTableCellControl));
        public static readonly RoutedEvent ClassSharedFileMenuItemClickEvent = EventManager.RegisterRoutedEvent("ClassSharedFileMenuItemClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ClassTableCellControl));
        public static readonly RoutedEvent SyllabusMenuItemClickEvent = EventManager.RegisterRoutedEvent("SyllabusMenuItemClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ClassTableCellControl));
        public static readonly RoutedEvent VideoMenuItemClickEvent = EventManager.RegisterRoutedEvent("VideoMenuItemClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ClassTableCellControl));

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public event RoutedEventHandler ClassContactButtonClick
        {
            add => AddHandler(ClassContactButtonClickEvent, value);
            remove => RemoveHandler(ClassContactButtonClickEvent, value);
        }

        public event RoutedEventHandler ReportButtonClick
        {
            add => AddHandler(ReportButtonClickEvent, value);
            remove => RemoveHandler(ReportButtonClickEvent, value);
        }

        public event RoutedEventHandler QuizButtonClick
        {
            add => AddHandler(QuizButtonClickEvent, value);
            remove => RemoveHandler(QuizButtonClickEvent, value);
        }

        public event RoutedEventHandler ClassSharedFileMenuItemClick
        {
            add => AddHandler(ClassSharedFileMenuItemClickEvent, value);
            remove => RemoveHandler(ClassSharedFileMenuItemClickEvent, value);
        }

        public event RoutedEventHandler SyllabusMenuItemClick
        {
            add => AddHandler(SyllabusMenuItemClickEvent, value);
            remove => RemoveHandler(SyllabusMenuItemClickEvent, value);
        }

        public event RoutedEventHandler VideoMenuItemClick
        {
            add => AddHandler(VideoMenuItemClickEvent, value);
            remove => RemoveHandler(VideoMenuItemClickEvent, value);
        }

        private void ClassContactButton_Click(object sender, RoutedEventArgs e)
        {
            RoutedEventArgs routedEventArgs = new(ClassContactButtonClickEvent);
            RaiseEvent(routedEventArgs);
        }

        private void ReportButton_Click(object sender, RoutedEventArgs e)
        {
            RoutedEventArgs routedEventArgs = new(ReportButtonClickEvent);
            RaiseEvent(routedEventArgs);
        }

        private void QuizButton_Click(object sender, RoutedEventArgs e)
        {
            RoutedEventArgs routedEventArgs = new(QuizButtonClickEvent);
            RaiseEvent(routedEventArgs);
        }

        private void ClassSharedFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            RoutedEventArgs routedEventArgs = new(ClassSharedFileMenuItemClickEvent);
            RaiseEvent(routedEventArgs);
        }

        private void SyllabusMenuItem_Click(object sender, RoutedEventArgs e)
        {
            RoutedEventArgs routedEventArgs = new(SyllabusMenuItemClickEvent);
            RaiseEvent(routedEventArgs);
        }

        private void VideoMenuItem_Click(object sender, RoutedEventArgs e)
        {
            RoutedEventArgs routedEventArgs = new(VideoMenuItemClickEvent);
            RaiseEvent(routedEventArgs);
        }

        private void FavoritesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var header = (string)(e.OriginalSource as MenuItem)!.Header;
            if (Regex.IsMatch(header, @"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)") || File.Exists(header) || Directory.Exists(header))
            {
                Process.Start(new ProcessStartInfo((string)(e.OriginalSource as MenuItem)!.Header) { UseShellExecute = true });
                Logger.Info($"Start Process {(string)(e.OriginalSource as MenuItem)!.Header}");
            }
        }

        private void FavoritesMenuItem_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                if (MessageBox.Show($"{(DataContext as ClassTableCell)!.SubjectsName}のお気に入りから削除しますか．\n{(string)(e.OriginalSource as MenuItem)!.Header}", "GakujoGUI", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    (DataContext as ClassTableCell)!.Favorites.Remove((string)(e.OriginalSource as MenuItem)!.Header);
                    Logger.Info($"Delete {(string)(e.OriginalSource as MenuItem)!.Header} favorite from {(DataContext as ClassTableCell)!.SubjectsName}.");
                    (Window.GetWindow(this) as MainWindow)!.RefreshClassTablesDataGrid(true);
                }
            }
        }

    }
}