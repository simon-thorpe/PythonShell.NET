PythonShell.NET
===============

.NET wrapper for the Python for Windows executable written in C#.

The purpose of this library is to run python code within the standard python install and get the result (via stdout). It was written to migrate a project that was using a python library to .NET.

### C# Example
```
PythonShell.PythonShell python = new PythonShell.PythonShell();

string result = python.Eval(@"
num = 5 + 3
print('hello number ' + str(8))
");

// result = "hello number 8"
```
The example above ran two lines of python code and returned the result to a string.
