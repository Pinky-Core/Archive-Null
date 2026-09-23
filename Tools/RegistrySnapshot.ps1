# Preserve the exact registry byte representation, including Unity float entries
# stored with a DWORD type but eight bytes of data on some editor versions.
if (-not ('ArchiveRegistryBytes' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
public static class ArchiveRegistryBytes {
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode)]
    private static extern int RegQueryValueEx(SafeRegistryHandle key, string name, IntPtr reserved, out uint type, byte[] data, ref uint size);
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode)]
    private static extern int RegSetValueEx(SafeRegistryHandle key, string name, int reserved, uint type, byte[] data, int size);
    public static byte[] Read(RegistryKey key,string name) {
        uint type, size=0; int error=RegQueryValueEx(key.Handle,name,IntPtr.Zero,out type,null,ref size);
        if(error!=0) throw new InvalidOperationException("Registry read failed: "+error);
        byte[] data=new byte[size]; error=RegQueryValueEx(key.Handle,name,IntPtr.Zero,out type,data,ref size);
        if(error!=0) throw new InvalidOperationException("Registry read failed: "+error); return data;
    }
    public static void Write(RegistryKey key,string name,uint type,byte[] data) {
        int error=RegSetValueEx(key.Handle,name,0,type,data,data.Length);
        if(error!=0) throw new InvalidOperationException("Registry restore failed: "+error);
    }
}
'@
}
function Restore-ArchiveRegistrySnapshot($key, $snapshot) {
    foreach ($name in $key.GetValueNames()) { if (!$snapshot.ContainsKey($name)) { $key.DeleteValue($name, $false) } }
    foreach ($name in $snapshot.Keys) {
        $entry = $snapshot[$name]
        if ($entry.ContainsKey('Raw')) { $bytes = [byte[]]$entry.Raw }
        elseif ($entry.Kind -eq [Microsoft.Win32.RegistryValueKind]::Binary) { $bytes = [byte[]]$entry.Value }
        elseif ($entry.Value -is [long]) { $bytes = [BitConverter]::GetBytes([long]$entry.Value) }
        elseif ($entry.Value -is [int]) { $bytes = [BitConverter]::GetBytes([int]$entry.Value) }
        else { throw "Snapshot has no raw representation for $name" }
        [ArchiveRegistryBytes]::Write($key,$name,[uint32]$entry.Kind,$bytes)
    }
}
