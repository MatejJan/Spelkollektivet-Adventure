using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace SpelkollektivetAdventure
{
    class Program
    {
        #region Variables

        // Enumerations

        enum Location
        {
            Nowhere,
            Inventory,
            Entrance,
            Lobby,
            Reception,
            RoundRoom,
            DiningRoom
        }

        enum Thing
        {
            None,
            All,
            Suitcase,
            James,
            Plate
        }

        enum Direction
        {
            North,
            NorthEast,
            East,
            SouthEast,
            South,
            SouthWest,
            West,
            NorthWest,
            Down,
            Up
        }

        // Data structures

        struct LocationData
        {
            public Location ID;
            public string Name;
            public string Description;
            public Dictionary<Direction, Location> Directions;
        }

        struct ThingData
        {
            public Thing ID;
            public string Name;
            public string Description;
            public Location StartingLocation;
        }

        struct ParsedData
        {
            public string ID;
            public string Name;
            public string Description;
            public Dictionary<Direction, Location> Directions;
            public Location StartingLocation;
        }

        // Data dictionaries

        static Dictionary<Location, LocationData> LocationsData = new Dictionary<Location, LocationData>();
        static Dictionary<Thing, ThingData> ThingsData = new Dictionary<Thing, ThingData>();

        // Vocabulary helpers

        static Dictionary<string, Thing> ThingsByName = new Dictionary<string, Thing>();
        static Thing[] ThingsYouCanTalkTo = { Thing.James };
        static Thing[] ThingsYouCanGet = { Thing.Suitcase, Thing.Plate };

        // Current state

        static Location CurrentLocation = Location.Entrance;
        static Dictionary<Thing, Location> ThingLocations = new Dictionary<Thing, Location>();

        // Helper variables

        static bool ShouldQuit = false;

        static ConsoleColor NarrativeColor = ConsoleColor.Gray;
        static ConsoleColor PromptColor = ConsoleColor.White;
        static int PrintPauseMilliseconds = 50;

        #endregion

        #region Program Start

        static void Main(string[] args)
        {
            // Initialize everything.
            ReadLocationsData();
            ReadThingsData();
            InitializeVocabularyHelpers();
            InitializeThingsState();

            // Display intro.
            Console.ForegroundColor = NarrativeColor;

            Print("Welcome to Spelkollektivet!");
            Print();
            Print("You are the newest homie in the house full of indie game developers. Your goal is to settle yourself in and get acquinted to the life in the house. Type 'checklist' to see the list of things you have to do by the end of the day. Your actions and conduct will be evaulated at the end. Press enter to begin.");

            Console.ReadKey();

            DisplayLocation();

            // Start the main interaction loop.
            while (!ShouldQuit)
            {
                PromptPlayer();
            }
        }

        // Initialization methods

        static void ReadLocationsData()
        {
            // Parse the locations file.
            List<ParsedData> parsedDataList = ParseData("Locations.txt");

            // Transfer data from the parsed structures into locations data.
            foreach (ParsedData parsedData in parsedDataList)
            {
                Location location = Enum.Parse<Location>(parsedData.ID);
                LocationData locationData = new LocationData
                {
                    ID = location,
                    Name = parsedData.Name,
                    Description = parsedData.Description,
                    Directions = parsedData.Directions
                };
                LocationsData[location] = locationData;
            }
        }

        static void ReadThingsData()
        {
            // Parse the things file.
            List<ParsedData> parsedDataList = ParseData("Things.txt");

            // Transfer data from the parsed structures into things data.
            foreach (ParsedData parsedData in parsedDataList)
            {
                Thing thing = Enum.Parse<Thing>(parsedData.ID);
                ThingData thingData = new ThingData
                {
                    ID = thing,
                    Name = parsedData.Name,
                    Description = parsedData.Description,
                    StartingLocation = parsedData.StartingLocation
                };
                ThingsData[thing] = thingData;
            }
        }

        static List<ParsedData> ParseData(string filePath)
        {
            var parsedDataList = new List<ParsedData>();

            string[] dataLines = File.ReadAllLines(filePath);
            var currentLineIndex = 0;

            // Parse data until we reach the end.
            while (currentLineIndex < dataLines.Length)
            {
                // First line of data holds the ID string.
                string id = dataLines[currentLineIndex];

                // Initialize the structure that will hold parsed data.
                var parsedData = new ParsedData
                {
                    ID = id,
                    Directions = new Dictionary<Direction, Location>()
                };

                // The remaining lines hold various properties in "property: value" or "property:" format.
                currentLineIndex++;

                do
                {
                    // Extract property and potentialy value.
                    MatchCollection matches = Regex.Matches(dataLines[currentLineIndex], @"(\w+): *(.*)?");
                    if (matches.Count == 0)
                    {
                        throw new FormatException("Invalid property line: " + dataLines[currentLineIndex]);
                    }

                    string property = matches[0].Groups[1].Value;
                    string value = matches[0].Groups[2]?.Value;

                    // Store value into data structure.
                    switch (property)
                    {
                        case "Name":
                            parsedData.Name = value;
                            break;

                        case "Description":
                            parsedData.Description = value;
                            break;

                        case "Directions":
                            // Directions are listed in separate lines with format "  direction: destination".
                            do
                            {
                                // Continue while the next line is a directions line.
                                MatchCollection directionsLineMatches = Regex.Matches(dataLines[currentLineIndex + 1], @"[ \t]+(\w+): *(.*)");
                                if (directionsLineMatches.Count == 0) break;

                                // Store parsed data into the directions dictionary.
                                Direction direction = Enum.Parse<Direction>(directionsLineMatches[0].Groups[1].Value);
                                Location destination = Enum.Parse<Location>(directionsLineMatches[0].Groups[2].Value);
                                parsedData.Directions[direction] = destination;

                                currentLineIndex++;

                            } while (currentLineIndex + 1 < dataLines.Length);
                            break;

                        case "StartingLocation":
                            parsedData.StartingLocation = Enum.Parse<Location>(value);
                            break;
                    }

                    currentLineIndex++;

                    // Keep parsing until we reach an empty line, which signifies the end of the current entry.
                } while (currentLineIndex < dataLines.Length && dataLines[currentLineIndex].Length > 0);

                // All data for this entry was parsed. Store it and skip the next empty line.
                parsedDataList.Add(parsedData);
                currentLineIndex++;
            }

            return parsedDataList;
        }

        static void InitializeVocabularyHelpers()
        {
            // Create a map of things by their name.
            foreach (KeyValuePair<Thing, ThingData> thingEntry in ThingsData)
            {
                ThingsByName[thingEntry.Value.Name.ToLowerInvariant()] = thingEntry.Key;
            }
        }

        static void InitializeThingsState()
        {
            // Set all things to their starting locations.
            foreach (KeyValuePair<Thing, ThingData> thingEntry in ThingsData)
            {
                ThingLocations[thingEntry.Key] = thingEntry.Value.StartingLocation;
            }
        }

        #endregion

        #region Output Helpers

        /// <summary>
        /// Writes an empty line to the output.
        /// </summary>
        static void Print()
        {
            Console.WriteLine();
            Thread.Sleep(PrintPauseMilliseconds);
        }

        /// <summary>
        /// Writes the specified text to the output.
        /// </summary>
        static void Print(string text)
        {
            // Split text into lines that don't exceed the window width.
            int maximumLineLength = Console.WindowWidth - 1;
            MatchCollection lineMatches = Regex.Matches(text, @"(.{1," + maximumLineLength + @"})(?:\s|$)");

            // Output each line with a small delay.
            foreach (Match match in lineMatches)
            {
                Console.WriteLine(match.Groups[0].Value);
                Thread.Sleep(PrintPauseMilliseconds);
            }
        }

        /// <summary>
        /// Returns the text with the first letter in uppercase.
        /// </summary>
        static string Capitalize(string text)
        {
            return text.Substring(0, 1).ToUpperInvariant() + text.Substring(1);
        }

        #endregion

        #region Interaction helpers

        /// <summary>
        /// Returns a list of things that are mentioned in the specified words.
        /// </summary>
        static List<Thing> GetThingsFromWords(string[] words)
        {
            List<Thing> things = new List<Thing>();

            // For each word, see if it's a name of a thing.
            foreach (string word in words)
            {
                if (ThingsByName.ContainsKey(word))
                {
                    things.Add(ThingsByName[word]);
                }
            }

            return things;
        }

        /// <summary>
        /// Returns all things that are at the specified location.
        /// </summary>
        static IEnumerable<Thing> GetThingsAtLocation(Location location)
        {
            return ThingLocations.Keys.Where(thing => ThingLocations[thing] == location);

            /* Below code kept to show students how much longer an imperative filter code it.

            List<Thing> thingsAtLocation = new List<Thing>();

            foreach (KeyValuePair<Thing, Location> thingEntry in ThingLocations)
            {
                if (thingEntry.Value == location)
                {
                    thingsAtLocation.Add(thingEntry.Key);
                }
            }

            return thingsAtLocation;
            */
        }

        /// <summary>
        /// Returns the names of specified things.
        /// </summary>
        static IEnumerable<string> GetThingNames(IEnumerable<Thing> things)
        {
            return things.Select(thing => ThingsData[thing].Name);

            /* Below code kept to show students how much longer an imperative map code it.
              
            string[] thingNames = new string[things.Count];

            for (var i = 0; i < things.Count; i++)
            {
                thingNames[i] = ThingsData[things[i]].Name;
            }

            return thingNames;
            */
        }

        /// <summary>
        /// Tells if any of the words are "everything" or "all".
        /// </summary>
        static bool WordsIncludeEverythingSynonyms(string[] words)
        {
            string[] everythingSynonyms = { "everything", "all" };

            return everythingSynonyms.Intersect(words).Count() > 0;
        }

        #endregion

        #region Interaction

        static void PromptPlayer()
        {
            // Ask the player what they want to do.
            Print("What now?");
            Print();

            Console.ForegroundColor = PromptColor;
            Console.Write("> ");

            string command = Console.ReadLine().ToLowerInvariant();

            Console.ForegroundColor = NarrativeColor;
            Print();

            // Analyze the command by assuming the first word is a verb (or similar instruction).
            string[] words = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0)
            {
                Reply("Try typing something.");
                return;
            }

            string verb = words[0];

            // Some commands are performed with things so see if any are being mentioned.
            List<Thing> things = GetThingsFromWords(words);

            // Call the appropriate handler for the given verb.
            switch (verb)
            {
                // Directions

                case "north":
                case "n":
                    HandleMovement(Direction.North);
                    break;

                case "northeast":
                case "ne":
                    HandleMovement(Direction.NorthEast);
                    break;

                case "east":
                case "e":
                    HandleMovement(Direction.East);
                    break;

                case "southeast":
                case "se":
                    HandleMovement(Direction.SouthEast);
                    break;

                case "south":
                case "s":
                    HandleMovement(Direction.South);
                    break;

                case "southwest":
                case "sw":
                    HandleMovement(Direction.SouthWest);
                    break;

                case "west":
                case "w":
                    HandleMovement(Direction.West);
                    break;

                case "northwest":
                case "nw":
                    HandleMovement(Direction.NorthWest);
                    break;

                // Common commands

                case "l":
                case "look":
                    HandleLook(words, things);
                    break;

                case "talk":
                    HandleTalk(words, things);
                    break;

                case "get":
                case "pick":
                case "take":
                    HandleGet(words, things);
                    break;

                case "drop":
                    HandleDrop(words, things);
                    break;

                case "i":
                case "inventory":
                    DisplayInventory();
                    break;

                // Special commands

                case "end":
                case "quit":
                    Reply("Goodbye!");
                    ShouldQuit = true;
                    break;

                default:
                    Reply("I do not understand you.");
                    break;
            }
        }

        // Verb handlers

        static void HandleMovement(Direction direction)
        {
            // See if the current location has the desired direction.
            LocationData currentLocationData = LocationsData[CurrentLocation];

            if (!currentLocationData.Directions.ContainsKey(direction))
            {
                Reply("You cannot go there.");
                return;
            }

            // Move the player to that location.
            CurrentLocation = currentLocationData.Directions[direction];

            // Display the new location's description.
            DisplayLocation();
        }

        static void HandleLook(string[] words, List<Thing> things)
        {
            if (words.Length == 1)
            {
                // This is the simple look command which just outputs the location description again.
                DisplayLocation();
                return;
            }

            // We're trying to look at things. See if any were mentioned.
            if (things.Count == 0)
            {
                Reply("I don't see it.");
                return;
            }

            // Display descriptions of all mentioned things.
            foreach (Thing thing in things)
            {
                DisplayThing(thing);
            }
        }

        static void HandleTalk(string[] words, List<Thing> things)
        {
            // Handle edge cases.
            if (words.Length == 1)
            {
                Reply("Talk to who?");
                return;
            }

            if (things.Count == 0)
            {
                Reply("I don't know who you mean.");
                return;
            }

            // Only consider the first thing mentioned.
            Thing thing = things[0];

            // Make sure the thing can be talked to.
            if (!ThingsYouCanTalkTo.Contains(thing))
            {
                Reply("You can't talk to that.");
                return;
            }

            // Make sure the thing is present.
            if (ThingLocations[thing] != CurrentLocation)
            {
                ThingData thingData = ThingsData[thing];
                Reply($"{thingData.Name} is not here.");
            }

            // Everything seems to be OK, proceed to the talk event with the specific person.
            switch (thing)
            {
                case Thing.James:
                    TalkToJames();
                    break;
            }
        }

        static void HandleGet(string[] words, List<Thing> things)
        {
            // Handle edge cases.
            if (words.Length == 1)
            {
                Reply("What do you want to get?");
                return;
            }

            // See if we want to get everything.
            if (WordsIncludeEverythingSynonyms(words))
            {
                IEnumerable<Thing> thingsAtLocation = GetThingsAtLocation(CurrentLocation);

                // Find the things we can actually get.
                IEnumerable<Thing> availableThingsToGet = ThingsYouCanGet.Intersect(thingsAtLocation);

                if (availableThingsToGet.Count() == 0)
                {
                    Reply("There is nothing here to be picked up.");
                    return;
                }

                // Move the things to your inventory.
                foreach (Thing thing in availableThingsToGet)
                {
                    GetThing(thing);
                }

                Reply("OK.");
                return;
            }

            // Make sure we understood what to get.
            if (things.Count == 0)
            {
                Reply("I don't know which thing you want to get.");
                return;
            }

            // Try to get all mentioned things.
            List<Thing> thingsPickedUp = new List<Thing>();

            foreach (Thing thing in things)
            {
                ThingData thingData = ThingsData[thing];

                // Make sure the thing can be picked up.
                if (!ThingsYouCanGet.Contains(thing))
                {
                    Reply($"{Capitalize(thingData.Name)} can't be picked up.");
                    continue;
                }

                // Check if you already have the thing.
                if (HaveThing(thing))
                {
                    Reply($"{Capitalize(thingData.Name)} is already in your possession.");
                    continue;
                }

                // Make sure the thing is at this location.
                if (!ThingIsHere(thing))
                {
                    Reply($"{Capitalize(thingData.Name)} is not here.");
                    continue;
                }

                // Everything seems to be OK, take the thing.
                GetThing(thing);
                thingsPickedUp.Add(thing);
            }

            // If nothing was picked up, we let the error messages speak for themselves.
            if (thingsPickedUp.Count == 0) return;

            // If everything was picked up, we simply confirm the command.
            if (thingsPickedUp.Count == things.Count)
            {
                Reply("OK.");
                return;
            }

            // It seems that some items weren't picked up, so in addition to the error messages we want to state what did get picked up.
            Reply($"You picked up {string.Join(", ", GetThingNames(thingsPickedUp))}.");
        }

        static void HandleDrop(string[] words, List<Thing> things)
        {
            // Handle edge cases.
            if (words.Length == 1)
            {
                Reply("What do you want to drop?");
                return;
            }

            // See if we want to drop everything.
            if (WordsIncludeEverythingSynonyms(words))
            {
                // See if we have any things to drop.
                IEnumerable<Thing> thingsInInventory = GetThingsAtLocation(Location.Inventory);

                if (thingsInInventory.Count() == 0)
                {
                    Reply("You aren't carrying anything.");
                    return;
                }

                // Drop all things.
                foreach (Thing thing in thingsInInventory)
                {
                    DropThing(thing);
                }

                Reply("OK.");
                return;
            }

            // Make sure we understood what to drop.
            if (things.Count == 0)
            {
                Reply("I don't know which thing you want to drop.");
                return;
            }

            // Try to drop all the mentioned things.
            List<Thing> thingsDropped = new List<Thing>();

            foreach (Thing thing in things)
            {
                ThingData thingData = ThingsData[thing];

                // Make sure you have the thing.
                if (!HaveThing(thing))
                {
                    Reply($"{Capitalize(thingData.Name)} is not in your inventory.");
                    continue;
                }

                // Everything seems to be OK, drop the item.
                DropThing(thing);
                thingsDropped.Add(thing);
            }

            // If nothing was dropped, we let the error messages speak for themselves.
            if (thingsDropped.Count == 0) return;

            // If everything was dropped, we simply confirm the command.
            if (thingsDropped.Count == things.Count)
            {
                Reply("OK.");
                return;
            }

            // It seems some items weren't dropped, so in addition to the error messages we want to state what did get dropped.
            Reply($"You dropped {string.Join(", ", GetThingNames(thingsDropped))}.");
        }

        #endregion

        #region Display methods

        /// <summary>
        /// Displays everything the player needs to know about the location.
        /// </summary>
        static void DisplayLocation()
        {
            // Display current location description.
            LocationData currentLocationData = LocationsData[CurrentLocation];

            Console.Clear();
            Print(currentLocationData.Description);
            Print();

            // Display possible directions.
            String directionsDescription = "Possible exits are:";

            foreach (KeyValuePair<Direction, Location> directionEntry in currentLocationData.Directions)
            {
                string directionName = directionEntry.Key.ToString().ToLowerInvariant();
                directionsDescription += $" {directionName}";
            }

            Print(directionsDescription + ".");

            // Display things that are at the location.
            Print("You see:");

            IEnumerable<Thing> thingsAtCurrentLocation = GetThingsAtLocation(CurrentLocation);

            if (thingsAtCurrentLocation.Count() == 0)
            {
                Print("    nothing.");
            }
            foreach (Thing thing in thingsAtCurrentLocation)
            {
                ThingData thingData = ThingsData[thing];
                Print($"    {thingData.Name}.");
            }
            Print();
        }

        /// <summary>
        /// Displays the specified thing's description.
        /// </summary>
        static void DisplayThing(Thing thing)
        {
            ThingData thingData = ThingsData[thing];
            Reply(thingData.Description);
        }

        /// <summary>
        /// Displays things in the inventory.
        /// </summary>
        static void DisplayInventory()
        {
            Print("You are carrying:");

            IEnumerable<Thing> thingsInInventory = GetThingsAtLocation(Location.Inventory);

            if (thingsInInventory.Count() == 0)
            {
                Print("    nothing.");
            }
            else
            {
                foreach (Thing thing in thingsInInventory)
                {
                    ThingData thingData = ThingsData[thing];
                    Print($"    {thingData.Name}.");
                }
            }

            Print();
        }

        #endregion

        #region Event helpers

        // Condition helpers

        /// <summary>
        /// Tells if the thing is at the specified location.
        /// </summary>
        static bool ThingAt(Thing thing, Location location)
        {
            return ThingLocations[thing] == location;
        }

        /// <summary>
        /// Tells if the thing is at the current location.
        /// </summary>
        static bool ThingIsHere(Thing thing)
        {
            return ThingAt(thing, CurrentLocation);
        }

        /// <summary>
        /// Tells if the things is either at the current location or in the inventory.
        /// </summary>
        static bool ThingAvailable(Thing thing)
        {
            return ThingIsHere(thing) || HaveThing(thing);
        }

        /// <summary>
        /// Tells if the thing is in the inventory.
        /// </summary>
        static bool HaveThing(Thing thing)
        {
            return ThingLocations[thing] == Location.Inventory;
        }

        // Action helpers

        /// <summary>
        /// Responds to player's command with the specified message.
        /// </summary>
        static void Reply(string message)
        {
            Print(message);
            Print();
        }

        /// <summary>
        /// Moves thing to desired location.
        /// </summary>
        static void MoveThing(Thing thing, Location location)
        {
            ThingLocations[thing] = location;
        }

        /// <summary>
        /// Places the thing into the inventory.
        /// </summary>
        static void GetThing(Thing thing)
        {
            MoveThing(thing, Location.Inventory);
        }

        /// <summary>
        /// Places the thing to the current location.
        /// </summary>
        static void DropThing(Thing thing)
        {
            MoveThing(thing, CurrentLocation);
        }

        #endregion

        #region Events

        static void TalkToJames()
        {
            if (ThingAt(Thing.James, Location.Lobby))
            {
                Reply("James says \"Welcome to Spelkollektivet! You'll first want to get settled in. Your room is on the west side of the second part of the north wing.\"");
                Reply("James points to the east and adds \"If you have any questions, I'll be in the reception.\"");
                Reply("James leaves southeast.");

                MoveThing(Thing.James, Location.Reception);
            }
            else
            {
                Reply("James looks busy and you don't want to bother him.");
            }
        }

        #endregion
    }
}
