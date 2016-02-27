# Branching

Short detour about AppVeyor config: all the settings are in the appveyor.yml file, only the build number comes from the AppVeyor project settings. The appveyor.yml adds the "-{branch}-{build}" string to the versions defined in GlobalAssemblyInfo.cs files' AssemblyInformationalVersion attribute. This version will be the build version also. Stable branch builds don't extend the versions defined in the GlobalAssemblyInfo.cs files.

We use a simplified version of the original GitFlow:

![Branching](https://camo.githubusercontent.com/f011896cab0a6e086954a10d3a5132d57ca69468/687474703a2f2f662e636c2e6c792f6974656d732f3369315a336e3154316b3339327231413351306d2f676974666c6f772d6d6f64656c2e3030312e706e67)

See: [Jeremy Helms - Branching](https://gist.github.com/digitaljhelms/4287848)

The original GitFlow:

![GitFlow](http://nvie.com/img/git-model@2x.png)

See: [Vincent Driessen - GitFlow](http://nvie.com/posts/a-successful-git-branching-model/) and [Jeff Kreeftmeijer - GitFlow commands](http://jeffkreeftmeijer.com/2010/why-arent-you-using-git-flow/)