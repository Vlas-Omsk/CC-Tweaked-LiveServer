---@type string
local _path
---@type string
local _httpEndPoint
---@type string
local _webSocketEndPoint
---@type Websocket
local _ws

---@class OutboundPacket
---@field Type OutboundPacketType
---@field Data any

---@enum OutboundPacketType
local OutboundPacketType = {
    EntryChanged = "EntryChanged",
    EntryTree = "EntryTree"
}

---@class InboundPacket
---@field Type InboundPacketType
---@field Data any

---@enum InboundPacketType
local InboundPacketType = {
    GetTree = "GetTree"
}

---@class ChangedEntryDTO
---@field ChangeType ChangeTypeDTO
---@field EntryType EntryTypeDTO
---@field Path string
---@field OldPath string
---@field ContentChanged boolean

---@enum ChangeTypeDTO
local ChangeTypeDTO = {
    Changed = "Changed",
    Created = "Created",
    Deleted = "Deleted",
    Moved = "Moved"
}

---@enum EntryTypeDTO
local EntryTypeDTO = {
    File = "File",
    Directory = "Directory"
}

---@class EntryDTO
---@field Path string
---@field EntryType EntryTypeDTO

function Connect()
    if _ws ~= nil then
        _ws.close()
    end

    local ws, reason

    repeat
        ws, reason = http.websocket(_webSocketEndPoint)

        if ws == false then
            print("Error while connecting to " .. _webSocketEndPoint .. ", reason: " .. reason .. ". Witing 10 seconds and reconnecting.")
            os.sleep(10)
        end
    until ws ~= false

    ---@diagnostic disable-next-line: cast-local-type
    _ws = ws
end

---@return OutboundPacket
function ReceivePacket()
    local message

    repeat
        message = _ws.receive()

        if message == nil then
            error("Error while receiving message")
        end
    until message ~= nil

    ---@diagnostic disable-next-line: param-type-mismatch
    return textutils.unserializeJSON(message)
end

---@param packet InboundPacket
function SendPacket(packet)
    local message = textutils.serializeJSON(packet)

    _ws.send(message, false)
end

---@param sourceUrl string
---@param destinationPath string
function Download(sourceUrl, destinationPath)
    local url = _httpEndPoint .. '/' .. sourceUrl

    print("Downloading: " .. url)

    local response, reason = http.get({
        url = url,
        binary = true
    })

    if response == nil then
        printError("Error while getting " .. url .. ", reason: " .. reason)
        return
    end

    print(response.getResponseCode())

    ---@type BinaryReadHandle
    ---@diagnostic disable-next-line: assign-type-mismatch
    local responseHandle = response
    ---@type BinaryWriteHandle
    ---@diagnostic disable-next-line: assign-type-mismatch
    local fileHandle = fs.open(destinationPath, 'wb')
    local data = responseHandle.readAll()

    if data == nil then
        printError("Empty response " .. url)
        return
    end

    fileHandle.write(data)

    -- Not work
    --
    -- repeat
    --     data = responseHandle.read(4096)
        
    --     if data ~= nil then
    --         fileHandle.write(data)
    --     end
    -- until data ~= nil

    fileHandle.close()
    responseHandle.close()

    print("Downloaded " .. url)
end

---@param entryType EntryTypeDTO
---@param path string
function CreatePath(entryType, path)
    if entryType == EntryTypeDTO.File then
      path = fs.getDir(path)
    end
    
    fs.makeDir(path)
end

---@param data ChangedEntryDTO
function OnEntryChanged(data)
    local path = fs.combine(_path, data.Path)

    if data.ChangeType == ChangeTypeDTO.Changed or data.ChangeType == ChangeTypeDTO.Created then
        CreatePath(data.EntryType, path)
    elseif data.ChangeType == ChangeTypeDTO.Deleted then
        fs.delete(path)
    elseif data.ChangeType == ChangeTypeDTO.Moved then
        local oldPath = fs.combine(_path, data.OldPath)

        if fs.exists(path) then
            fs.delete(path)
        end

        fs.move(oldPath, path)
    else
        error("Unknown ChangeType")
    end

    if data.ContentChanged == true then
        Download(data.Path, path)
    end
end

---@param data EntryDTO[]
function OnEntryTree(data)
    for _, v in pairs(data) do
        local path = fs.combine(_path, v.Path)

        CreatePath(v.EntryType, path)

        if (v.EntryType == EntryTypeDTO.File) then
            Download(v.Path, path)
        end
    end
end

function StartReceivingLoop()
    while true do
        local packet = ReceivePacket()

        print('Message received: ' .. packet.Type)

        if packet.Type == OutboundPacketType.EntryTree then
            OnEntryTree(packet.Data)
        elseif packet.Type == OutboundPacketType.EntryChanged then
            OnEntryChanged(packet.Data)
        else
            error("Unknown OutboundPacketType")
        end
    end
end

function Start()
    Connect()

    SendPacket({
        Type = InboundPacketType.GetTree
    })

    StartReceivingLoop()
end

function Main()
    local programPath = shell.getRunningProgram()
    local programDirectory = fs.getDir(programPath)

    _path = arg[1]
    local host = arg[2]

    if _path == nil then
        print('Path is nil')
        return
    end

    if host == nil then
        print('Host is nil')
        return
    end

    if arg[3] == nil then
        if shell.openTab ~= nil then
            local cmd = '/' .. programPath .. ' ' .. _path .. ' ' .. host .. ' false'
            shell.openTab(cmd)
            return
        end
    end

    _path = fs.combine(programDirectory, _path)
    _httpEndPoint = 'http://' .. host
    _webSocketEndPoint = 'ws://' .. host

    print("Path: " .. _path)

    local result

    while true do
        _, result = pcall(Start)

        if result == "Terminated" then
            print("Ladno poka")

            break
        end

        printError("Error: " .. result .. ". Restart 10 seconds")
        os.sleep(10)
    end
end

Main()