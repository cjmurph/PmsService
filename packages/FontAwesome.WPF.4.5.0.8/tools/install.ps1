param($installPath, $toolsPath, $package)

$DTE.ItemOperations.Navigate("https://github.com/charri/Font-Awesome-WPF/?" + $package.Id + "=" + $package.Version)