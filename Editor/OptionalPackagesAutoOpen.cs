#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class OptionalPackagesAutoOpen
{
    // Đổi theo package name của bạn
    private const string PackageName = "com.yeager.essentialcore";

    // Key lưu trạng thái đã show (theo version)
    private static string PrefKeyShown(string version) =>
        $"{PackageName}.OptionalInstaller.Shown.{version}";

    static OptionalPackagesAutoOpen()
    {
        // Chạy sau khi Unity load/compile xong
        EditorApplication.delayCall += TryOpen;
    }

    private static void TryOpen()
    {
        // Lấy version hiện tại của package (đọc từ package.json)
        string version = PackageVersionUtil.GetPackageVersion(PackageName) ?? "unknown";

        // Đã mở rồi thì thôi
        if (EditorPrefs.GetBool(PrefKeyShown(version), false))
            return;

        // Mở window
        OptionalPackagesInstallerWindow.Open();

        // Đánh dấu đã mở cho version này
        EditorPrefs.SetBool(PrefKeyShown(version), true);
    }
}

internal static class PackageVersionUtil
{
    public static string GetPackageVersion(string packageName)
    {
        // Trả version từ Unity Package Manager cache (nếu có)
        // Cách đơn giản: dùng PackageInfo.FindForAssembly
        var asm = typeof(OptionalPackagesAutoOpen).Assembly;
        var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(asm);
        if (info != null && info.name == packageName)
            return info.version;

        // fallback: null
        return null;
    }
}
#endif