using FileStruct.Core.Models;

namespace FileStruct.Core.Interfaces;

public interface ISignatureMatcher
{
    List<SignatureMatch> Match(byte[] headerBytes);
    void AddUserRule(SignatureDefinition rule);
    void ClearUserRules();
}
