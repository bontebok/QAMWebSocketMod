# QAMWebSocketMod
A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/) used to create and update a QuadArrayMesh by using JSON values sent over a WebSocket connection.

## Usage

If using the Mod on a graphical client, ensure that the ResoniteModSettings Mod is also installed and edit the Allowed URIs property to include a list of all permitted URIs, or if you are on a headless client, edit the allowedUris value in the ```rml_config\QAMWebSocketMod.json``` file to include a comma separated list of allowed URIs for WebSocket connections from the Mod. Note: If this list is empty or the URI value is not found in the list, the connection will be blocked.

In Resonite, create a new Empty object and populated it with a Dynamic Variable Space named ```QAMWebSocket```.

Create the following three variables somewhere in that space -

```
QAMWebSocket/Uri <Uri>
QAMWebSocket/Slot <Slot>
QAMWebSocket/MeshRenderer <MeshRenderer>
```

For the Uri value variable, this value should be the URI this QuadArrayMesh will be connecting to and updating its data from. For the Slot reference variable, this will be the slot where the Mod will create the QuadArrayMesh onto, for the MeshRenderer reference variable, this will be the reference variable for the MeshRenderer that will be used to display the QuadArrayMesh.

Simply save and respawn this object to activate the Mod and open a WebSocket connection.

## Sending QuadArrayMesh line updates

To initialize a QuadArrayMesh, send the following JSON via the WebSocket:

```
{
    "type": "init",
    "width": 320,
    "height": 240
}
```

Where the width and height is the size for the QuadArrayMesh. The Mod will create the QuadArrayMesh automatically in the QAMWebSocket/Slot location using the width and height specified in the init message.

To send a link update to the QuadArrayMesh, send the following JSON via the WebSocket:

```
{
    "type": "line",
    "y": 20,   // <0 - height>
    "colors": <Base64 encoded RGB values for a row of width size>
}
```

The ```colors``` value must contain a Base64 encoded array of RGB values. Each RGB value should be 24 bits long (8 bits per color) and is encoded in a packed binary form. Currently only full line updates are supported.

