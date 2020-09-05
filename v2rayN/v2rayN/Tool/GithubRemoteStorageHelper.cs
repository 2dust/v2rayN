using Octokit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using v2rayN.Mode;
using v2rayN.Protos.Statistics;

namespace v2rayN.Tool {
    public static class GithubRemoteStorageHelper {
        private static GitHubClient GetClient(GithubRemoteStorageConfig config) {
            var client = new GitHubClient(new ProductHeaderValue(config.userName));
            var auth = new Credentials(config.token);
            client.Credentials = auth;
            return client;
        }
        private static async Task<RepositoryContent> GetFile(this GitHubClient client, Repository repo, string path) {
            string errorMessage = "";
            IReadOnlyList<RepositoryContent> files = null;
            try {
                files = await client.Repository.Content.GetAllContentsByRef(repo.Id, path, repo.DefaultBranch);
            }
            catch (Exception e) {
                errorMessage = e.Message;
                files = null;
            }
            return files?.FirstOrDefault();
        }
        private static async Task CreateFile(this GitHubClient client, Repository repo, string text, string path) {
            var createFileReq = new CreateFileRequest("V2Ray config changed(Initial).", text, repo.DefaultBranch, true);
            await client.Repository.Content.CreateFile(repo.Id, path, createFileReq);
        }
        private static async Task UpdateFile(this GitHubClient client, Repository repo, string text, string path, string sha) {
            var updateFileReq = new UpdateFileRequest("V2Ray config changed.", text, sha, true);
            await client.Repository.Content.UpdateFile(repo.Id, path, updateFileReq);
        }
        /// <summary>
        /// upload
        /// </summary>
        /// <param name="localVmessItems"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        public static async Task Upload(IList<VmessItem> localVmessItems, GithubRemoteStorageConfig config) {
            if (localVmessItems?.Any() != true) {
                return;
            }
            var vmessesJson = Newtonsoft.Json.JsonConvert.SerializeObject(localVmessItems);
            var client = GetClient(config);
            var repo = await client.Repository.Get(config.userName, config.repoName);
            var remoteFile = await client.GetFile(repo, config.path);
            if (remoteFile == null) {
                await client.CreateFile(repo, vmessesJson, config.path);
            }
            else {
                var md5 = MD5.Create();
                if (remoteFile.Content.Length != vmessesJson.Length || md5.ComputeHash(Encoding.UTF8.GetBytes(remoteFile.Content)) != md5.ComputeHash(Encoding.UTF8.GetBytes(vmessesJson))) {
                    await client.UpdateFile(repo, vmessesJson, config.path, remoteFile.Sha);
                }
            }
        }

        /// <summary>
        /// fetch
        /// </summary>
        /// <param name="localVmessItems"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        public static async Task Fetch(IList<VmessItem> localVmessItems, GithubRemoteStorageConfig config) {
            var client = GetClient(config);
            var repo = await client.Repository.Get(config.userName, config.repoName);
            var file = await client.GetFile(repo, config.path);
            if (file != null) {
                MD5 md5 = MD5.Create();

                try {

                    var remoteVmessItems = Newtonsoft.Json.JsonConvert.DeserializeObject<IList<VmessItem>>(file.Content);

                    var remoteVmessItemMD5Pairs = remoteVmessItems.Select(vmessItem => {
                        var rawText = Newtonsoft.Json.JsonConvert.SerializeObject(vmessItem);
                        var textBytes = Encoding.UTF8.GetBytes(rawText);
                        return new KeyValuePair<string, VmessItem>(Encoding.UTF8.GetString(md5.ComputeHash(textBytes)), vmessItem);
                    });

                    var localVmessItemMD5s = localVmessItems.Select(vmessItem => {
                        var rawText = Newtonsoft.Json.JsonConvert.SerializeObject(vmessItem);
                        var textBytes = Encoding.UTF8.GetBytes(rawText);
                        return Encoding.UTF8.GetString(md5.ComputeHash(textBytes));
                    }).Distinct().ToList();

                    foreach (var remoteVmessItem in remoteVmessItemMD5Pairs) {
                        if (!localVmessItemMD5s.Contains(remoteVmessItem.Key)) {
                            localVmessItems.Add(remoteVmessItem.Value);
                        }
                    }

                }
                catch {
                    UI.ShowWarning(UIRes.I18N("GithubRemoteStorageConfigDamaged"));
                    throw;
                }
            }
            else {
                //OperationFailed
                UI.ShowWarning(UIRes.I18N("GithubRemoteStorageConfigNotFound"));
                throw new Exception(UIRes.I18N("GithubRemoteStorageConfigNotFound"));
            }
        }
    }
}
