// MSI_SetActionSequence.js <msi-file> <table> <action> <sequence>
// Performs a post-build fixup of an msi to set the specified table/action/sequence

// Constant values from Windows Installer SDK
var msiOpenDatabaseModeTransact = 1;

var msiViewModifyInsert         = 1;
var msiViewModifyUpdate         = 2;
var msiViewModifyAssign         = 3;
var msiViewModifyReplace        = 4;
var msiViewModifyDelete         = 6;

if (WScript.Arguments.Length != 4)
{
    WScript.StdErr.WriteLine("Usage: " + WScript.ScriptName + " file table action sequence");
    WScript.Quit(1);
}

var filespec = WScript.Arguments(0);
var table = WScript.Arguments(1);
var action = WScript.Arguments(2);
var sequence = parseInt(WScript.Arguments(3));

var installer = WScript.CreateObject("WindowsInstaller.Installer");
var database = installer.OpenDatabase(filespec, msiOpenDatabaseModeTransact);

WScript.StdOut.WriteLine("Looking for action:" + action);

try
{   
    var sql = "SELECT Action, Sequence FROM " + table + " WHERE Action = '" + action + "'";
    var view = database.OpenView(sql);	

    view.Execute();		
    var record = view.Fetch();	

    if (record)
    {		
    	while (record)
    	{
    		WScript.StdOut.Write("Found: " + record.StringData(0) + ", " + record.StringData(1) + ", " + record.StringData(2));
    		if (record.IntegerData(2) != sequence)
    		{
    			WScript.StdOut.WriteLine(" - changing to " + sequence);
    			record.IntegerData(2) = sequence;
    			view.Modify(msiViewModifyUpdate,record);
    		}
    		else
    			WScript.StdOut.WriteLine(" - OK");

    		record = view.Fetch();
    	}

    	view.Close();
    	database.Commit();
    }
    else
    {			
    	view.Close();	
    	throw("Warning - Could not find " + table + "." + action);
    }
}
catch(e)
{
    WScript.StdErr.WriteLine(e);
    WScript.Quit(1);
}