﻿using MinecraftLaunch.Modules.Models.Download;
using MinecraftLaunch.Modules.Toolkits;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.ObjectModel;
using wonderlab.Class.AppData;
using wonderlab.Class.Enum;
using wonderlab.Class.Models;
using wonderlab.Class.Utils;

namespace wonderlab.ViewModels.Pages {
    public class NetConfigPageViewModel : ViewModelBase {
        public NetConfigPageViewModel() {
            PropertyChanged += OnPropertyChanged;

            if (GlobalResources.LauncherData.IsNull()) {
                GlobalResources.LauncherData = GlobalResources.DefaultLauncherData;
            }

            try {
                DownloadCount = GlobalResources.LauncherData.DownloadCount;
            }
            catch (NullReferenceException ex) {
                GlobalResources.LauncherData = GlobalResources.DefaultLauncherData;
                ex.ShowLog();
            }
        }

        private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName is nameof(DownloadCount)) {
                GlobalResources.LauncherData.DownloadCount = DownloadCount;
            }
        }

        [Reactive]
        public ObservableCollection<WebConnectionTestModel> TestList { get; set; } = new();

        [Reactive]
        public bool TestListVisible { get; set; } = false;

        [Reactive]
        public int DownloadCount { get; set; }

        public void RunConnectionTestAction() {
            TestList.Clear();
            TestListVisible = true;

            //下载源
            TestList.Add(new WebConnectionTestModel("https://bmclapi2.bangbang93.com"));
            TestList.Add(new WebConnectionTestModel("https://download.mcbbs.net"));
            TestList.Add(new WebConnectionTestModel("http://launchermeta.mojang.com"));

            //皮肤
            TestList.Add(new WebConnectionTestModel("http://textures.minecraft.net"));
            TestList.Add(new WebConnectionTestModel("https://sessionserver.mojang.com"));
            TestList.Add(new WebConnectionTestModel("https://www.minecraft.net", "minecraft.net"));

            //更新服务
            TestList.Add(new WebConnectionTestModel("http://43.136.86.16:8888/", "update.wonderapi.com"));
        }

        public void SelectLsaacAction() {
            GlobalResources.LauncherData.IssuingBranch = IssuingBranch.Lsaac;
        }

        public void SelectAlbertAction() {
            GlobalResources.LauncherData.IssuingBranch = IssuingBranch.Albert;
        }

        public void SelectOfficialAction() {
            GlobalResources.LauncherData.CurrentDownloadAPI = DownloadApiType.Mojang;
            APIManager.Current = APIManager.Mojang;
        }

        public void SelectMcbbsAction() {
            GlobalResources.LauncherData.CurrentDownloadAPI = DownloadApiType.Mcbbs;
            APIManager.Current = APIManager.Mcbbs;
        }

        public void SelectBmclAction() {
            GlobalResources.LauncherData.CurrentDownloadAPI = DownloadApiType.Bmcl;
            APIManager.Current = APIManager.Bmcl;
        }
    }
}
