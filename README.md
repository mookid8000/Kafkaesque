# Kafkaesque

It's a file reader/writer. It's a little bit how Kafka manages its files, so it's Kafkaesque.

With this, you can pretty efficiently write lines to a file, which can then be read in parallel by any number of readers.

## How could it look?

No matter what you want to do, you start out by `new`ing up a `LogDirectory` - and then you use it to get a writer or a reader.

### Writing

The writer requires exclusive access to the directory, so it will acquire a handle to a lock file in it, failing if it cannot
acquire it within 20 seconds.

The writer is meant to be acquired once, when you application starts up, and then DISPOSED when the application shuts down
again. As a single-method code example, usage could look like this:

```csharp
var logDirectory = new LogDirectory(@"C:\data\kafkaesque");

// hold on to this bad boy until your application shuts down
using(var logWriter = logDirectory.GetWriter())
{
    await logWriter.WriteAsync(new byte[] {1, 2, 3});
}

```

to write a little byte array to the file.

In an actual application, you would most likely use a `ConcurrentStack<IDisposable>` or an IoC container to
keep the writer instance around.


### Reading

You can have as many readers as you want, and they are created in an analoguous manner:

```csharp
var logDirectory = new LogDirectory(@"C:\data\kafkaesque");

var logReader = logDirectory.GetReader();

foreach (var logEvent in logReader.Read())
{
    var bytes = logEvent.Data;

    // process the bytes here
}
```

PLEASE NOTE that `Read` NEVER FINISHES READING, so you would normally pass a `CancellationToken` to it, which
gets cancelled when your application shuts down:

```csharp
var logDirectory = new LogDirectory(@"C:\data\kafkaesque");

var logReader = logDirectory.GetReader();

foreach (var logEvent in logReader.Read(cancellationToken: cancellationToken))
{
    var bytes = logEvent.Data;

    // process the bytes here
}
```

The great thing about readers, is that the `logEvent` above has _position information_ on it, allowing you to 
_resume reading from where you left off the last time_, if you store the position information somewhere.

With Kafkaesque, positions come in the form of a file number and a byte position, and they can be retrieved
from each `LogEvent` returned from the reader:

```csharp
var fileNumber = -1;    //< this assumes we haven't
var bytePosition = -1;  //< read anything before

foreach (var logEvent in logReader.Read(fileNumber: fileNumber, bytePosition: bytePosition, cancellationToken: cancellationToken))
{
    var bytes = logEvent.Data;

    fileNumber = logEvent.FileNumber;
    bytePosition = logEvent.BytePosition;

    // store the file number and the byte position in your database
}
```
