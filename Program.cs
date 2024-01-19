using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SharpZzZz;

public class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBox(IntPtr hWnd, string lpText, string lpCaption, uint uType);


    public static void DllMain()
    {
        MessageBox(0, $"HELLO FROM PROCESS -> {Process.GetCurrentProcess().ProcessName}", "ITS WORKS!!!", 0);
        throw new NotImplementedException(); // close notepad
    }

    static void Main(string[] args)
    {
        if (Process.GetCurrentProcess().ProcessName != "SharpZzZz")
            DllMain();
        else
            Zygote.Create(Process.Start("notepad.exe"));
    }
}
