﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;

#pragma warning disable CS0649

namespace ManualAutoUpdater {
	internal static class Program {
		private static readonly WebClient WebClient = new WebClient();

		private const string UpdatesUrl = "https://raw.githubusercontent.com/projectatom/manual_auto_updater/divinity/updates.json";

		private static readonly string ProfilesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) +
		                                              "/Larian Studios/Divinity Original Sin 2 Definitive Edition/PlayerProfiles/";

		private static readonly string ModsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) +
		                                          "/Larian Studios/Divinity Original Sin 2 Definitive Edition/Mods/";

		private static void Main() {
			Console.OutputEncoding = Encoding.UTF8;
			WebClient.Headers.Add("Referer: https://www.curseforge.com/");
			WebClient.Headers.Add("User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:105.0) Gecko/20100101 Firefox/105.0");

			var version = File.Exists("version.pickar") ? File.ReadAllText("version.pickar") : "no_version";

			var updatesString = WebClient.DownloadString(UpdatesUrl);
			var serializer = JsonSerializer.Create();
			var updates = serializer.Deserialize<UpdatesJson>(new JsonTextReader(new StringReader(updatesString)));

			var preparedRemovals = new HashSet<string>();
			var preparedDownloads = new HashSet<FileWithUrl>();

			var hasUpdates = false;
			var lastVersion = version;
			while (true) {
				var update = updates?.Updates.FirstOrDefault(u => u.From == lastVersion);
				if (update is null) break;
				hasUpdates = true;

				foreach (var remove in update.Removes) {
					var removeFile = new FileWithUrl {
						Name = remove
					};
					if (!preparedDownloads.Remove(removeFile)) preparedRemovals.Add(remove);
				}

				foreach (var fileWithUrl in update.Adds) preparedDownloads.Add(fileWithUrl);
				lastVersion = update.To;
			}

			if (hasUpdates) {
				Console.WriteLine($"Обновление с {version} до {lastVersion}");

				foreach (var preparedRemoval in preparedRemovals) RemoveFile(preparedRemoval);
				foreach (var fileWithUrl in preparedDownloads) {
					DownloadFile(fileWithUrl.Url, fileWithUrl.Name, out var error);
					if (error == string.Empty) continue;

					Console.WriteLine("ОШИБКА!!!");
					Console.WriteLine(error);
					Console.WriteLine("Для продолжения нажмите любую клавишу...");
					Console.ReadKey();
					return;
				}

				version = lastVersion;
			}

			using (var versionFile = File.CreateText("version.pickar")) versionFile.Write(version);

			Console.WriteLine(hasUpdates ? $"Обновлено до версии {version}" : $"Текущая версия {version}, обновлений не найдено");
			Console.WriteLine("Для продолжения нажмите любую клавишу...");
			Console.ReadKey();
		}

		private static string ReplaceVariables(string str, out string error) {
			error = string.Empty;
			if (str.Contains("%DivinityProfile%")) {
				var folder = Path.GetDirectoryName(ProfilesPath);
				if (!Directory.Exists(folder)) {
					error = "Запусти Дивинити хотя бы один раз и создай там профиль.";
					return str;
				}

				var profileNames = Directory.EnumerateDirectories(folder).Select(path => Path.GetFileName(path)).ToArray();
				if (!profileNames.Any()) {
					error = "Запусти Дивинити хотя бы один раз и создай там профиль.";
					return str;
				}

				var profileNamesWithoutDebug = profileNames.Where(profName => !profName.Contains("Debug")).ToArray();

				var profileName = profileNamesWithoutDebug.Any() ? profileNamesWithoutDebug[0] : profileNames[0];

				str = Path.GetFullPath(str.Replace("%DivinityProfile%", $@"{ProfilesPath}/{profileName}/"));
			}

			if (str.Contains("%DivinityMods%")) {
				Directory.CreateDirectory(ModsPath);

				str = Path.GetFullPath(str.Replace("%DivinityMods%", $@"{ModsPath}/"));
			}

			return str;
		}

		public static void DownloadFile(string url, string fileName, out string error) {
			Console.WriteLine($"Скачиваю {url}");
			fileName = ReplaceVariables(fileName, out error);
			if (error != string.Empty) { return; }

			var directoryName = Path.GetDirectoryName(fileName);
			if (directoryName != null) Directory.CreateDirectory(directoryName);
			WebClient.DownloadFile(url, fileName);
		}

		public static void RemoveFile(string fileName) {
			Console.WriteLine($"Удаляю {fileName}");
			if (File.Exists(fileName)) File.Delete(fileName);
		}
	}

	internal class UpdatesJson {
		public Update[] Updates;
	}

	internal class Update {
		public string[] Removes;
		public FileWithUrl[] Adds;
		public string From;
		public string To;
	}

	internal class FileWithUrl {
		public string Name;
		public string Url;

		public override int GetHashCode() {
			return Name.GetHashCode();
		}

		public override bool Equals(object obj) {
			return obj != null && obj.GetType() == typeof(FileWithUrl) && Name.Equals(((FileWithUrl) obj).Name);
		}
	}
}