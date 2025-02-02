using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using MinecraftLaunch.Modules.Interface;
using MinecraftLaunch.Modules.Models.Download;
using MinecraftLaunch.Modules.Models.Install;
using MinecraftLaunch.Modules.Models.Launch;
using MinecraftLaunch.Modules.Parser;
using MinecraftLaunch.Modules.Toolkits;
using Natsurainko.Toolkits.IO;
using Natsurainko.Toolkits.Network;
using Natsurainko.Toolkits.Network.Model;
using static System.Net.Mime.MediaTypeNames;

namespace MinecraftLaunch.Modules.Installer;

public class ResourceInstaller {
    private string Cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "wonderlab", "temp");

    public GameCore GameCore { get; set; }

    public List<IResource> FailedResources { get; set; } = new List<IResource>();

    public static int MaxDownloadThreads { get; set; } = 64;

    public async Task<ResourceInstallResponse> DownloadAsync(Action<string, float> func) {
        var progress = new Progress<(string, float)>();
        void Progress_ProgressChanged(object _, (string, float) e) => func(e.Item1, e.Item2);
        progress.ProgressChanged += Progress_ProgressChanged!;

        var manyBlock = new TransformManyBlock<List<IResource>, IResource>(x => x.Where(x => {
            if (string.IsNullOrEmpty(x.CheckSum) && x.Size == 0)
                return false;
            if (x.ToFileInfo().Verify(x.CheckSum) && x.ToFileInfo().Verify(x.Size))
                return false;

            return true;
        }));

        int post = 0;
        int output = 0;

        var actionBlock = new ActionBlock<IResource>(async resource => {
            post++;
            var request = resource.ToDownloadRequest();

            if (!request.Directory.Exists) {
                request.Directory.Create();
            }

            try {
                var file = Path.Combine(Cache, request.Directory.Name, request.FileName);
                var folder = new FileInfo(file).Directory;

                if (!request.Join().IsFile() && !File.Exists(file)) {
                    var httpDownloadResponse = await HttpWrapper.HttpDownloadAsync(request);

                    if (httpDownloadResponse.HttpStatusCode is HttpStatusCode.OK) {
                        if (!folder!.Exists) {
                            folder.Create();
                        }

                        if (!File.Exists(file)) {
                            httpDownloadResponse.FileInfo.CopyTo(file);
                        }
                    } else {
                        this.FailedResources.Add(resource);
                    }
                } else if (!request.Join().IsFile() && File.Exists(file)) {
                    File.Copy(file,Path.Combine(request.Directory.FullName, request.FileName));
                }
            }
            catch {
                this.FailedResources.Add(resource);
            }

            output++;

            ((IProgress<(string, float)>)progress).Report(($"{output}/{post}", output / (float)post));
        }, new ExecutionDataflowBlockOptions {
            BoundedCapacity = MaxDownloadThreads,
            MaxDegreeOfParallelism = MaxDownloadThreads
        });
        var disposable = manyBlock.LinkTo(actionBlock, new DataflowLinkOptions { PropagateCompletion = true });

        manyBlock.Post(this.GameCore.LibraryResources!.Where(x => x.IsEnable && !x.ToFileInfo().Exists).Select(x => (IResource)x).ToList());
        manyBlock.Post(this.GetFileResources().Where(x=> !x.ToFileInfo().Exists).ToList());
        manyBlock.Post(await this.GetAssetResourcesAsync());

        manyBlock.Complete();

        await actionBlock.Completion;
        disposable.Dispose();
        GC.Collect();

        progress.ProgressChanged -= Progress_ProgressChanged!;

        return new ResourceInstallResponse {
            FailedResources = this.FailedResources,
            SuccessCount = post - this.FailedResources.Count,
            Total = post
        };
    }

    public IEnumerable<IResource> GetFileResources() {
        if (GameCore.ClientFile != null)
            yield return GameCore.ClientFile;
    }

    public async ValueTask<List<IResource>> GetAssetResourcesAsync() {
        if (!(GameCore.AssetIndexFile!.FileInfo.Verify(GameCore.AssetIndexFile.Size) || GameCore.AssetIndexFile.FileInfo.Verify(GameCore.AssetIndexFile.CheckSum))) {

            var request = this.GameCore.AssetIndexFile.ToDownloadRequest();

            if (!request.Directory.Exists)
                request.Directory.Create();

            var res = await HttpWrapper.HttpDownloadAsync(request);
        }

        var entity = new AssetJsonEntity();
        entity = entity.FromJson(await File.ReadAllTextAsync(this.GameCore.AssetIndexFile?.ToFileInfo()!.FullName!));

        return new AssetParser(entity, this.GameCore.Root!).GetAssets().Select(x => (IResource)x).ToList();
    }

    public async static ValueTask<List<IResource>> GetAssetFilesAsync(GameCore core) {
        var asset = new AssetParser(new AssetJsonEntity().FromJson(await File.ReadAllTextAsync(core.AssetIndexFile.ToFileInfo().FullName)), core.Root).GetAssets().Select((Func<AssetResource, IResource>)((AssetResource x) => x)).ToList();
        var res = core.LibraryResources.Where((LibraryResource x) => x.IsEnable).Select((Func<LibraryResource, IResource>)((LibraryResource x) => x)).ToList();
        res.AddRange(asset);
        res.Sort((x, x1) => x.Size.CompareTo(x1.Size));
        return res;
    }

    [Obsolete]
    public IEnumerable<IResource> GetModLoaderResourcesAsync() {
        var entity = new GameCoreJsonEntity().FromJson(File.ReadAllText(Path.Combine(this.GameCore.Root.FullName, "versions", this.GameCore.Id, $"{this.GameCore.Id}.json")));
        List<LibraryResource> list = entity.Libraries.Select(x => {
            return new LibraryResource() {
                Root = GameCore.Root,
                Name = x.Name,
                Size = x.Name.Length,
                CheckSum = "114514",
                Url = x.Url,
            };
        }).ToList();

        foreach (var i in list) {
            if (!i.ToFileInfo().Exists) {
                yield return i;
            }
        }
    }

    public ResourceInstaller(GameCore core) {
        GameCore = core;
    }
}
