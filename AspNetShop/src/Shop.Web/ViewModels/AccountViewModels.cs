using System.ComponentModel.DataAnnotations;

namespace Shop.Web.ViewModels;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Укажите имя")]
    [StringLength(100)]
    [Display(Name = "Имя")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите e-mail")]
    [EmailAddress(ErrorMessage = "Некорректный e-mail")]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите пароль")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Пароль должен быть не короче 6 символов")]
    [DataType(DataType.Password)]
    [Display(Name = "Пароль")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Пароли не совпадают")]
    [Display(Name = "Повторите пароль")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Укажите e-mail")]
    [EmailAddress(ErrorMessage = "Некорректный e-mail")]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите пароль")]
    [DataType(DataType.Password)]
    [Display(Name = "Пароль")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Запомнить меня")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class ProfileViewModel
{
    [Required(ErrorMessage = "Укажите имя")]
    [StringLength(100)]
    [Display(Name = "Имя")]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Некорректный телефон")]
    [Display(Name = "Телефон")]
    public string? Phone { get; set; }

    [StringLength(300)]
    [Display(Name = "Адрес доставки")]
    public string? Address { get; set; }

    public string Email { get; set; } = string.Empty;
}
