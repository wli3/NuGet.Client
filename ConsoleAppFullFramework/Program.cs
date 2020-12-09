using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Common;

namespace ConsoleAppFullFramework
{
    class Program
    {
        static void Main(string[] args)
        {
            var platform = "Windows";
            if (RuntimeEnvironmentHelper.IsMacOSX)
            {
                platform = "macOs";
            }
            else if (RuntimeEnvironmentHelper.IsLinux)
            {
                platform = "linux";
            }
            else if (RuntimeEnvironmentHelper.IsMono)
            {
                platform = "mono";
            }
            Console.WriteLine($"Testing on {platform} , the framework is full framework \n");
            var compareResult = "";

            var ProgramFilesX86_NET = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86);
            var ProgramFilesX86_NuGet = NuGetEnvironment.GetFolderPath(NuGetEnvironment.SpecialFolder.ProgramFilesX86);
            compareResult = String.Equals(ProgramFilesX86_NET, ProgramFilesX86_NuGet) ? "same" : "different";
            Console.WriteLine($"Compare ProgramFilesX86 , they are {compareResult} ");
            Console.WriteLine($"ProgramFilesX86 in .NET,  {ProgramFilesX86_NET} ");
            Console.WriteLine($"ProgramFilesX86 in NuGet,  {ProgramFilesX86_NuGet} \n");

            var ProgramFiles_NET = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles);
            var ProgramFiles_NuGet = NuGetEnvironment.GetFolderPath(NuGetEnvironment.SpecialFolder.ProgramFiles);
            compareResult = String.Equals(ProgramFiles_NET, ProgramFiles_NuGet) ? "same" : "different";
            Console.WriteLine($"Compare ProgramFiles , they are {compareResult} ");
            Console.WriteLine($"ProgramFiles in .NET,  {ProgramFiles_NET} ");
            Console.WriteLine($"ProgramFiles in NuGet,  {ProgramFiles_NuGet} \n");

            var UserProfile_NET = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            var UserProfile_NuGet = NuGetEnvironment.GetFolderPath(NuGetEnvironment.SpecialFolder.UserProfile);
            compareResult = String.Equals(UserProfile_NET, UserProfile_NuGet) ? "same" : "different";
            Console.WriteLine($"Compare UserProfile , they are {compareResult} ");
            Console.WriteLine($"UserProfile in .NET,  {UserProfile_NET} ");
            Console.WriteLine($"UserProfile in NuGet,  {UserProfile_NuGet}  \n");

            var CommonApplicationData_NET = System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData);
            var CommonApplicationData_NuGet = NuGetEnvironment.GetFolderPath(NuGetEnvironment.SpecialFolder.CommonApplicationData);
            compareResult = String.Equals(CommonApplicationData_NET, CommonApplicationData_NuGet) ? "same" : "different";
            Console.WriteLine($"Compare CommonApplicationData , they are {compareResult} ");
            Console.WriteLine($"CommonApplicationData in .NET,  {CommonApplicationData_NET} ");
            Console.WriteLine($"CommonApplicationData in NuGet,  {CommonApplicationData_NuGet} \n");

            var ApplicationData_NET = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
            var ApplicationData_NuGet = NuGetEnvironment.GetFolderPath(NuGetEnvironment.SpecialFolder.ApplicationData);
            compareResult = String.Equals(ApplicationData_NET, ApplicationData_NuGet) ? "same" : "different";
            Console.WriteLine($"Compare ApplicationData, they are {compareResult} ");
            Console.WriteLine($"ApplicationData in .NET,  {ApplicationData_NET} ");
            Console.WriteLine($"ApplicationData in NuGet,  {ApplicationData_NuGet} \n");

            var LocalApplicationData_NET = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
            var LocalApplicationData_NuGet = NuGetEnvironment.GetFolderPath(NuGetEnvironment.SpecialFolder.LocalApplicationData);
            compareResult = String.Equals(LocalApplicationData_NET, LocalApplicationData_NuGet) ? "same" : "different";
            Console.WriteLine($"Compare ApplicationData, they are {compareResult} ");
            Console.WriteLine($"LocalApplicationData in .NET,  {LocalApplicationData_NET} ");
            Console.WriteLine($"LocalApplicationData in NuGet,  {LocalApplicationData_NuGet} \n");
        }

    }
}
