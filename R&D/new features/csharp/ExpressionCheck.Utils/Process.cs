// For examples, see:
// https://thegraybook.vvvv.org/reference/extending/writing-nodes.html#examples

namespace Main;

[ProcessNode]
public class Process
{
    private int _counter;

    public int Update(int increment)
    {
        return _counter += increment;
    }
}