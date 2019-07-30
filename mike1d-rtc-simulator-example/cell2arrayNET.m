function b = cell2arrayNET(a)
% Convert cell array 'a' to .NET-array 'b'
    b = NET.createArray('System.String',length(a));
    for i = 1:length(a)
        b.Set(i-1, a{i});
    end
end