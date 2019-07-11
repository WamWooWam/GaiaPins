# GAiA Pins

This is a Discord bot that copies pinned messages to their own channel via a webhook.

## Prerequisites
 * .NET Core 3.0 SDK (preview)

## Setup
 * Clone the Repo
 * Move `example.appsettings.json` to `appsettings.json` and set the bot token in that file.
 * `dotnet run`
 * ...
 * Profit

## Usage
There are 3 main commands you need to use this bot

### `p;enable <channel> [<webhook_url>]`
Enables pin redirection for the server, moving pins into the specified channel. For this command to work, the bot needs Manage Webhook permissions in the target channel, or `webhook_url` must be specified and valid.

### `p;migrate`
This command migrates all pins from all channels on the server into the specified pins channel. You should probably run this command immediately after `p;setup`, but this isn't done automatically, just in case.

### `p;disable`
Disables pin migration for the server, but leaves all channels and webhooks intact. It's important to note that this comamnd does clear the list of known moved messages for the server.