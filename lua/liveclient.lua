---@type string
local _httpEndPoint
---@type string
local _webSocketEndPoint
---@type websocket
local _ws

function EnsureConnected()
    local ws, d = http.websocket("")

    if _ws.GetOpened() == true then
        _ws.Close()
    end
end

function Main()
    local programPath = shell.getRunningProgram()
    local programDirectory = fs.getDir(programPath)

    local path = arg[1]
    local host = arg[2]
    local port = arg[3]

    if path == nil then
        print('Path is nil')
    end

    if host == nil then
        print('Host is nil')
    end

    _httpEndPoint = 'http://' .. host
    _webSocketEndPoint = 'ws://' .. host

    if port ~= nil then
        _httpEndPoint = _httpEndPoint .. ':' .. port .. '/'
        _webSocketEndPoint = _webSocketEndPoint .. ':' .. port .. '/'
    end

    while true do
        
    end
end

Main()