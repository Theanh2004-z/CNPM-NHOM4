using Microsoft.AspNetCore.Identity;

namespace TuyenSinhOnline.Security;

public sealed class VietnameseIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => Error(nameof(DefaultError),
        "Không thể thực hiện thao tác. Vui lòng thử lại.");

    public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail),
        "Email này đã được sử dụng.");

    public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName),
        "Email này đã được sử dụng.");

    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail),
        "Email không hợp lệ.");

    public override IdentityError InvalidUserName(string? userName) => Error(nameof(InvalidUserName),
        "Email chứa ký tự không được hỗ trợ.");

    public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort),
        $"Mật khẩu cần ít nhất {length} ký tự.");

    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric),
        "Mật khẩu cần có ký tự đặc biệt.");

    public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit),
        "Mật khẩu cần có chữ số.");

    public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower),
        "Mật khẩu cần có chữ thường.");

    public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper),
        "Mật khẩu cần có chữ hoa.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars),
        $"Mật khẩu cần có ít nhất {uniqueChars} ký tự khác nhau.");

    public override IdentityError ConcurrencyFailure() => Error(nameof(ConcurrencyFailure),
        "Dữ liệu vừa thay đổi. Vui lòng thử lại.");

    private static IdentityError Error(string code, string description) => new()
    {
        Code = code,
        Description = description
    };
}
