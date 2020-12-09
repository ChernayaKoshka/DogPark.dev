namespace DogPark.Authentication

open System
open System.Linq
open System.Collections.Concurrent
open System.Collections.Immutable
open System.Security.Claims
open System.IdentityModel.Tokens.Jwt
open Microsoft.IdentityModel.Tokens
open System.Security.Cryptography
open DogPark.Shared

type JwtAuthManager(privateRsaParams: RSAParameters, issuer, audience, accessTokenExpiration, refreshTokenExpiration) =
    let usersRefreshTokens = new ConcurrentDictionary<string, RefreshToken>()

    member this.UsersRefreshTokensReadOnlyDictionary with get() = usersRefreshTokens.ToImmutableDictionary()

    member this.GenerateRefreshTokenString() =
        let randomNumber: byte array = Array.zeroCreate 128
        use rng = RandomNumberGenerator.Create()
        rng.GetBytes(randomNumber)
        Convert.ToBase64String(randomNumber)

    member this.GenerateTokens username (claims: Claim[]) (now: DateTime) =
        let shouldAddAudienceClaim =
            claims
            |> Array.exists (fun claim ->
                claim.Type = JwtRegisteredClaimNames.Aud
            )
            |> not

        let jwtToken =
            JwtSecurityToken(
                issuer = issuer,
                audience = (if shouldAddAudienceClaim then audience else String.Empty),
                claims = claims,
                expires = now.Add(accessTokenExpiration),
                signingCredentials = SigningCredentials(RsaSecurityKey(privateRsaParams), SecurityAlgorithms.RsaSha256Signature)
            )

        let accessToken = JwtSecurityTokenHandler().WriteToken(jwtToken)

        let refreshToken =
            {
                Username = username
                TokenString = this.GenerateRefreshTokenString()
                ExpireAt = now.Add(refreshTokenExpiration)
            }

        usersRefreshTokens.AddOrUpdate(username, refreshToken, (fun s t -> refreshToken))
        |> ignore

        {
            AccessToken = accessToken
            RefreshToken = refreshToken
        }

    member this.DecodeJwtToken token validateLifetime =
        try
            if String.IsNullOrWhiteSpace(token) then
                None
            else
            let principal, validatedToken =
                JwtSecurityTokenHandler()
                    .ValidateToken(token,
                        TokenValidationParameters(
                            ValidateIssuer = true,
                            ValidIssuer = issuer,
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = RsaSecurityKey(privateRsaParams),
                            ValidAudience = audience,
                            ValidateAudience = true,
                            ValidateLifetime = validateLifetime,
                            ClockSkew = TimeSpan.FromMinutes(1.)
                        ))
            Some (principal, validatedToken :?> JwtSecurityToken)
        with
        | e ->
            printfn "%A" e
            None

    member this.Refresh refreshToken accessToken now =
        let decodeResult = this.DecodeJwtToken accessToken false
        match decodeResult with
        | Some (principal, jwtToken) when jwtToken.Header.Alg = SecurityAlgorithms.RsaSha256Signature ->
            match usersRefreshTokens.TryGetValue(principal.Identity.Name) with
            | (true, existingRefreshToken) when
                    existingRefreshToken.TokenString = refreshToken
                    && existingRefreshToken.Username = principal.Identity.Name
                    && existingRefreshToken.ExpireAt > now
                ->
                this.GenerateTokens principal.Identity.Name (Array.ofSeq principal.Claims) now
                |> Some
            | _ ->
                None
        | _ ->
            None

    member this.Logout (username: string) =
        username
        |> usersRefreshTokens.TryRemove
        |> fst