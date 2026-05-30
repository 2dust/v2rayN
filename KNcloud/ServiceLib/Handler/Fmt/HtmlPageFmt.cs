namespace ServiceLib.Handler.Fmt;

public class HtmlPageFmt : BaseFmt
{
    public static bool IsHtmlPage(string strData)
    {
        return Contains(strData, "<html", "<!doctype html", "<head");
    }
}
