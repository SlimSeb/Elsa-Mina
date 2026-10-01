using Autofac;
using ElsaMina.Core.Handlers;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Logging;

namespace ElsaMina.Core.Utils;

public static class ContainerBuilderExtensions
{
    public static void RegisterCommand<TCommand>(this ContainerBuilder builder) where TCommand : ICommand
    {
        var commandAttribute = typeof(TCommand).GetCommandAttribute();
        if (commandAttribute == null)
        {
            Log.Warning(
                "Command '{0}' does not have the named command attribute, and could not be registered",
                typeof(TCommand).Name);
            return;
        }

        var commandName = commandAttribute.Name;
        if (string.IsNullOrEmpty(commandName))
        {
            Log.Warning("Command '{0}' has no name, and could not be registered", typeof(TCommand).Name);
            return;
        }

        // Commands hold no per-call state, so one instance serves every call. ICommandRegistry indexes it
        // under its name and aliases.
        builder.RegisterType<TCommand>().As<ICommand>().SingleInstance();
    }

    public static void RegisterHandler<THandler>(this ContainerBuilder builder) where THandler : IHandler
    {
        builder.RegisterType<THandler>().As<IHandler>().SingleInstance();
    }
}