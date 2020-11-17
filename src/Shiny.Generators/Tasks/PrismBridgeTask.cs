﻿//using System;
//using System.Linq;
//using Microsoft.CodeAnalysis;


//namespace Shiny.Generators.Tasks
//{
//    public class PrismBridgeTask : ShinySourceGeneratorTask
//    {
//        public override void Execute()
//        {
//            var apps = this.Context.GetAllDerivedClassesForType("Prism.DryIoc.PrismApplication", true);

//            switch (apps.Count())
//            {
//                case 0:
//                    this.Log.Info("Prism.DryIoc.PrismApplication not found");
//                    break;

//                case 1:
//                    var app = apps.First();
//                    var builder = new IndentedStringBuilder();
//                    builder.AppendNamespaces("Prism.Ioc", "Prism.Mvvm", "Prism.DryIoc", "DryIoc");

//                    var prismAssembly = this.Context.Compilation.ReferencedAssemblyNames.FirstOrDefault(x => x.Name.Equals("Prism."));
//                    var isPrism8 = (prismAssembly?.Version.Major ?? 7) >= 8;

//                    using (builder.BlockInvariant("namespace " + app.ContainingNamespace.Name))
//                    {
//                        using (builder.BlockInvariant("public partial class " + app.Name))
//                        {
//                            using (builder.BlockInvariant("protected override IContainerExtension CreateContainerExtension()"))
//                            {
//                                builder.AppendLineInvariant("var container = new Container(this.CreateContainerRules());");

//                                if (isPrism8)
//                                {
//                                    builder.Append(@"Shiny.ShinyHost.Populate((serviceType, func, lifetime) => container.Register(serviceType, func));");
//                                }
//                                else
//                                {
//                                    builder.Append(@"Shiny.ShinyHost.Populate((serviceType, func, lifetime) => container.RegisterDelegate(serviceType, _ => func(), Reuse.Singleton));");
//                                }

//                                if (!this.ShinyContext.IsStartupGenerated)
//                                {
//                                    builder.AppendLineInvariant("Xamarin.Forms.Internals.DependencyResolver.ResolveUsing(t => ShinyHost.Container.GetService(t));");
//                                    builder.AppendLine();
//                                }
//                                builder.AppendLineInvariant("return new DryIocContainerExtension(container);");
//                                builder.AppendLine();
//                            }
//                        }
//                    }
//                    this.Context.AddSource(app.Name, builder.ToString());
//                    break;

//                default:
//                    this.Log.Warn("More than 1 PrismApplication found");
//                    break;
//            }
//        }
//    }
//}
