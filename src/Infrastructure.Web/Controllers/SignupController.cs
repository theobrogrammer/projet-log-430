/*
[HttpPost("signup")]
public async Task<ActionResult<SignupResponseDto>> Signup([FromBody] SignupRequestDto req) {
    // 1) valider DTO (ModelState ou FluentValidation)
    if (!ModelState.IsValid) return UnprocessableEntity(ModelState);

    // 2) mapper vers le port entrant (Application)
    var result = await _signupUseCase.CreateAccountAsync(new SignupParams(
        email: req.Email,
        phone: req.Phone,
        fullName: req.FullName,
        birthDate: req.BirthDate));

    // 3) mapper le résultat vers le ResponseDTO
    return Ok(new SignupResponseDto {
        ClientId = result.ClientId,
        AccountId = result.AccountId,
        Status = result.Status
    });
}
*/ 